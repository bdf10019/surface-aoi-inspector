from __future__ import annotations

import argparse
import json
import os
import random
import sys
from dataclasses import asdict, dataclass
from pathlib import Path

import numpy as np
import onnxruntime as ort
import torch
import torch.nn.functional as functional
from torch import nn
from torch.utils.data import DataLoader, WeightedRandomSampler
from tqdm import tqdm

from dataset import KSDD2Dataset, SampleRecord, discover_samples, stratified_split
from model import SurfaceDefectSegmenter


@dataclass
class Metrics:
    loss: float
    accuracy: float
    precision: float
    recall: float
    specificity: float
    balanced_accuracy: float
    pixel_dice: float


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train KSDD2 surface-defect segmenter")
    parser.add_argument("--data-root", type=Path, default=Path("data/raw/KolektorSDD2"))
    parser.add_argument("--epochs", type=int, default=5)
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--learning-rate", type=float, default=1e-3)
    parser.add_argument("--height", type=int, default=256)
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--validation-ratio", type=float, default=0.15)
    parser.add_argument("--workers", type=int, default=0)
    parser.add_argument("--output-dir", type=Path, default=Path("ai/runs/default"))
    parser.add_argument("--max-train-samples", type=int)
    parser.add_argument("--max-validation-samples", type=int)
    parser.add_argument("--no-pretrained", action="store_true")
    return parser.parse_args()


def seed_everything(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


def limit_records(
    records: list[SampleRecord], maximum: int | None, seed: int
) -> list[SampleRecord]:
    if maximum is None or len(records) <= maximum:
        return records
    positives = [record for record in records if record.has_defect]
    negatives = [record for record in records if not record.has_defect]
    generator = random.Random(seed)
    generator.shuffle(positives)
    generator.shuffle(negatives)
    positive_count = min(len(positives), max(1, maximum // 2))
    selected = positives[:positive_count] + negatives[: maximum - positive_count]
    generator.shuffle(selected)
    return selected


def create_training_loader(
    records: list[SampleRecord], image_size: tuple[int, int], batch_size: int, workers: int
) -> DataLoader:
    dataset = KSDD2Dataset(records, image_size=image_size, augment=True)
    positive_count = sum(record.has_defect for record in records)
    negative_count = len(records) - positive_count
    weights = [
        1.0 / (positive_count if record.has_defect else negative_count)
        for record in records
    ]
    sampler = WeightedRandomSampler(weights, num_samples=len(records), replacement=True)
    return DataLoader(
        dataset,
        batch_size=batch_size,
        sampler=sampler,
        num_workers=workers,
        pin_memory=torch.cuda.is_available(),
    )


def create_validation_loader(
    records: list[SampleRecord], image_size: tuple[int, int], batch_size: int, workers: int
) -> DataLoader:
    dataset = KSDD2Dataset(records, image_size=image_size, augment=False)
    return DataLoader(
        dataset,
        batch_size=batch_size,
        shuffle=False,
        num_workers=workers,
        pin_memory=torch.cuda.is_available(),
    )


def segmentation_loss(logits: torch.Tensor, targets: torch.Tensor) -> torch.Tensor:
    positive_weight = torch.tensor([8.0], device=logits.device)
    binary_cross_entropy = functional.binary_cross_entropy_with_logits(
        logits, targets, pos_weight=positive_weight
    )
    probabilities = torch.sigmoid(logits)
    intersection = (probabilities * targets).sum(dim=(1, 2, 3))
    denominator = probabilities.sum(dim=(1, 2, 3)) + targets.sum(dim=(1, 2, 3))
    dice_loss = 1.0 - ((2.0 * intersection + 1.0) / (denominator + 1.0)).mean()
    return (0.65 * binary_cross_entropy) + (0.35 * dice_loss)


def train_epoch(
    model: nn.Module,
    loader: DataLoader,
    optimizer: torch.optim.Optimizer,
    scaler: torch.amp.GradScaler,
    device: torch.device,
) -> float:
    model.train()
    total_loss = 0.0
    use_amp = device.type == "cuda"
    for images, masks in tqdm(loader, desc="train", leave=False):
        images = images.to(device, non_blocking=True)
        masks = masks.to(device, non_blocking=True)
        optimizer.zero_grad(set_to_none=True)
        with torch.autocast(device_type=device.type, dtype=torch.float16, enabled=use_amp):
            loss = segmentation_loss(model(images), masks)
        scaler.scale(loss).backward()
        scaler.step(optimizer)
        scaler.update()
        total_loss += loss.item() * images.shape[0]
    return total_loss / len(loader.dataset)


@torch.inference_mode()
def evaluate(model: nn.Module, loader: DataLoader, device: torch.device) -> Metrics:
    model.eval()
    total_loss = 0.0
    true_positive = true_negative = false_positive = false_negative = 0
    pixel_intersection = 0.0
    pixel_total = 0.0

    for images, masks in tqdm(loader, desc="validate", leave=False):
        images = images.to(device, non_blocking=True)
        masks = masks.to(device, non_blocking=True)
        logits = model(images)
        total_loss += segmentation_loss(logits, masks).item() * images.shape[0]
        predictions = torch.sigmoid(logits) >= 0.5
        expected = masks.flatten(1).any(dim=1)
        minimum_pixels = max(1, round(masks.shape[-2] * masks.shape[-1] * 0.0005))
        predicted = predictions.flatten(1).sum(dim=1) >= minimum_pixels

        true_positive += int((predicted & expected).sum())
        true_negative += int((~predicted & ~expected).sum())
        false_positive += int((predicted & ~expected).sum())
        false_negative += int((~predicted & expected).sum())
        pixel_intersection += float((predictions & masks.bool()).sum()) * 2.0
        pixel_total += float(predictions.sum() + masks.sum())

    total = true_positive + true_negative + false_positive + false_negative
    precision = divide(true_positive, true_positive + false_positive)
    recall = divide(true_positive, true_positive + false_negative)
    specificity = divide(true_negative, true_negative + false_positive)
    return Metrics(
        loss=total_loss / len(loader.dataset),
        accuracy=divide(true_positive + true_negative, total),
        precision=precision,
        recall=recall,
        specificity=specificity,
        balanced_accuracy=(recall + specificity) / 2.0,
        pixel_dice=pixel_intersection / max(1.0, pixel_total),
    )


def divide(numerator: int, denominator: int) -> float:
    return numerator / denominator if denominator else 0.0


def export_onnx(
    model: nn.Module, output_path: Path, image_size: tuple[int, int], device: torch.device
) -> None:
    model.eval()
    sample = torch.zeros(1, 3, *image_size, device=device)
    torch.onnx.export(
        model,
        (sample,),
        output_path,
        input_names=["image"],
        output_names=["defect_logits"],
        opset_version=18,
        dynamo=True,
    )
    session = ort.InferenceSession(str(output_path), providers=["CPUExecutionProvider"])
    result = session.run(None, {"image": np.zeros((1, 3, *image_size), dtype=np.float32)})[0]
    if result.shape != (1, 1, *image_size):
        raise RuntimeError(f"Unexpected ONNX output shape: {result.shape}")


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    arguments = parse_arguments()
    project_root = Path(__file__).resolve().parents[1]
    os.environ.setdefault("TORCH_HOME", str(project_root / ".torch"))
    seed_everything(arguments.seed)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    image_size = (arguments.height, arguments.width)
    output_dir = arguments.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    records = discover_samples(arguments.data_root / "train")
    training_records, validation_records = stratified_split(
        records, arguments.validation_ratio, arguments.seed
    )
    training_records = limit_records(
        training_records, arguments.max_train_samples, arguments.seed
    )
    validation_records = limit_records(
        validation_records, arguments.max_validation_samples, arguments.seed + 1
    )
    training_loader = create_training_loader(
        training_records, image_size, arguments.batch_size, arguments.workers
    )
    validation_loader = create_validation_loader(
        validation_records, image_size, arguments.batch_size, arguments.workers
    )

    print(
        json.dumps(
            {
                "device": str(device),
                "gpu": torch.cuda.get_device_name(0) if device.type == "cuda" else None,
                "training_images": len(training_records),
                "validation_images": len(validation_records),
                "image_size": image_size,
            },
            ensure_ascii=False,
        )
    )

    model = SurfaceDefectSegmenter(
        pretrained_backbone=not arguments.no_pretrained
    ).to(device)
    optimizer = torch.optim.AdamW(
        model.parameters(), lr=arguments.learning_rate, weight_decay=1e-4
    )
    scaler = torch.amp.GradScaler("cuda", enabled=device.type == "cuda")
    history: list[dict] = []
    best_balanced_accuracy = -1.0
    checkpoint_path = output_dir / "best.pt"

    for epoch in range(1, arguments.epochs + 1):
        training_loss = train_epoch(model, training_loader, optimizer, scaler, device)
        metrics = evaluate(model, validation_loader, device)
        row = {"epoch": epoch, "training_loss": training_loss, **asdict(metrics)}
        history.append(row)
        print(json.dumps(row))
        if metrics.balanced_accuracy > best_balanced_accuracy:
            best_balanced_accuracy = metrics.balanced_accuracy
            torch.save(
                {
                    "model_state": model.state_dict(),
                    "image_size": image_size,
                    "metrics": row,
                },
                checkpoint_path,
            )

    checkpoint = torch.load(checkpoint_path, map_location=device, weights_only=True)
    model.load_state_dict(checkpoint["model_state"])
    onnx_path = output_dir / "surface_defect_segmenter.onnx"
    export_onnx(model, onnx_path, image_size, device)
    summary = {
        "device": str(device),
        "gpu": torch.cuda.get_device_name(0) if device.type == "cuda" else None,
        "best": checkpoint["metrics"],
        "history": history,
        "checkpoint": str(checkpoint_path),
        "onnx": str(onnx_path),
    }
    (output_dir / "metrics.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary["best"]))
    print(f"ONNX_READY {onnx_path}")


if __name__ == "__main__":
    main()
