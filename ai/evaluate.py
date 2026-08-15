from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from dataclasses import asdict
from pathlib import Path

import numpy as np
import onnxruntime as ort
import torch
from torch.utils.data import DataLoader
from tqdm import tqdm

from dataset import KSDD2Dataset, discover_samples
from model import SurfaceDefectSegmenter
from train import evaluate


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Evaluate a trained KSDD2 model on the untouched official test split"
    )
    parser.add_argument("--data-root", type=Path, default=Path("data/raw/KolektorSDD2"))
    parser.add_argument("--run-dir", type=Path, default=Path("ai/runs/ksdd2-v1"))
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--workers", type=int, default=0)
    parser.add_argument("--latency-samples", type=int, default=100)
    return parser.parse_args()


def benchmark_onnx(
    model_path: Path,
    dataset: KSDD2Dataset,
    sample_count: int,
) -> dict[str, float | int | str]:
    session = ort.InferenceSession(
        str(model_path.resolve()), providers=["CPUExecutionProvider"]
    )
    input_name = session.get_inputs()[0].name
    timings: list[float] = []
    count = min(sample_count, len(dataset))

    for index in tqdm(range(count), desc="onnx-cpu", leave=False):
        image, _ = dataset[index]
        input_tensor = image.unsqueeze(0).numpy().astype(np.float32, copy=False)
        started = time.perf_counter()
        output = session.run(None, {input_name: input_tensor})[0]
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        if output.shape != (1, 1, *dataset.image_size):
            raise RuntimeError(f"Unexpected ONNX output shape: {output.shape}")
        if index > 0:
            timings.append(elapsed_ms)

    return {
        "provider": session.get_providers()[0],
        "measured_images": len(timings),
        "median_ms": statistics.median(timings),
        "mean_ms": statistics.fmean(timings),
        "p95_ms": float(np.percentile(timings, 95)),
    }


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    arguments = parse_arguments()
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    checkpoint_path = arguments.run_dir / "best.pt"
    onnx_path = arguments.run_dir / "surface_defect_segmenter.onnx"
    checkpoint = torch.load(checkpoint_path, map_location=device, weights_only=True)
    image_size = tuple(checkpoint["image_size"])

    records = discover_samples(arguments.data_root / "test")
    dataset = KSDD2Dataset(records, image_size=image_size, augment=False)
    loader = DataLoader(
        dataset,
        batch_size=arguments.batch_size,
        shuffle=False,
        num_workers=arguments.workers,
        pin_memory=device.type == "cuda",
    )

    model = SurfaceDefectSegmenter(pretrained_backbone=False).to(device)
    model.load_state_dict(checkpoint["model_state"])
    metrics = evaluate(model, loader, device)
    latency = benchmark_onnx(onnx_path, dataset, arguments.latency_samples)
    result = {
        "split": "official_test",
        "images": len(records),
        "positive_images": sum(record.has_defect for record in records),
        "negative_images": sum(not record.has_defect for record in records),
        "image_size": list(image_size),
        "pytorch_device": str(device),
        "metrics": asdict(metrics),
        "onnx_cpu_latency": latency,
        "decision_rule": {
            "pixel_probability_threshold": 0.5,
            "minimum_defect_area_ratio": 0.0005,
        },
    }
    output_path = arguments.run_dir / "test_metrics.json"
    output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    print(f"TEST_METRICS_READY {output_path.resolve()}")


if __name__ == "__main__":
    main()
