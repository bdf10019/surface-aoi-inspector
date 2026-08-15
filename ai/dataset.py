from __future__ import annotations

import random
from dataclasses import dataclass
from pathlib import Path

import torch
from PIL import Image
from torch.utils.data import Dataset
from torchvision.transforms import functional as transform
from torchvision.transforms.functional import InterpolationMode


@dataclass(frozen=True)
class SampleRecord:
    image_path: Path
    mask_path: Path
    has_defect: bool


def discover_samples(folder: Path) -> list[SampleRecord]:
    records: list[SampleRecord] = []
    for image_path in sorted(folder.glob("*.png")):
        if "_GT" in image_path.stem:
            continue
        mask_path = image_path.with_name(f"{image_path.stem}_GT.png")
        if not mask_path.exists() and image_path.stem.endswith(" (copy)"):
            base_stem = image_path.stem.removesuffix(" (copy)")
            mask_path = image_path.with_name(f"{base_stem}_GT (copy).png")
        if not mask_path.exists():
            raise FileNotFoundError(f"Missing ground-truth mask: {mask_path}")
        with Image.open(mask_path) as mask:
            has_defect = mask.convert("L").getbbox() is not None
        records.append(SampleRecord(image_path, mask_path, has_defect))
    if not records:
        raise RuntimeError(f"No KSDD2 samples found in {folder}")
    return records


def stratified_split(
    records: list[SampleRecord], validation_ratio: float, seed: int
) -> tuple[list[SampleRecord], list[SampleRecord]]:
    generator = random.Random(seed)
    positive = [record for record in records if record.has_defect]
    negative = [record for record in records if not record.has_defect]
    generator.shuffle(positive)
    generator.shuffle(negative)

    def split(group: list[SampleRecord]) -> tuple[list[SampleRecord], list[SampleRecord]]:
        validation_count = max(1, round(len(group) * validation_ratio))
        return group[validation_count:], group[:validation_count]

    positive_train, positive_validation = split(positive)
    negative_train, negative_validation = split(negative)
    training = positive_train + negative_train
    validation = positive_validation + negative_validation
    generator.shuffle(training)
    generator.shuffle(validation)
    return training, validation


class KSDD2Dataset(Dataset[tuple[torch.Tensor, torch.Tensor]]):
    def __init__(
        self,
        records: list[SampleRecord],
        image_size: tuple[int, int],
        augment: bool,
    ) -> None:
        self.records = records
        self.image_size = image_size
        self.augment = augment

    def __len__(self) -> int:
        return len(self.records)

    def __getitem__(self, index: int) -> tuple[torch.Tensor, torch.Tensor]:
        record = self.records[index]
        with Image.open(record.image_path) as source:
            image = source.convert("RGB")
        with Image.open(record.mask_path) as source:
            mask = source.convert("L")

        image_tensor = transform.to_tensor(image)
        mask_tensor = transform.pil_to_tensor(mask).float() / 255.0
        image_tensor = transform.resize(
            image_tensor,
            self.image_size,
            interpolation=InterpolationMode.BILINEAR,
            antialias=True,
        )
        mask_tensor = transform.resize(
            mask_tensor,
            self.image_size,
            interpolation=InterpolationMode.NEAREST,
        )
        mask_tensor = (mask_tensor > 0.5).float()

        if self.augment:
            if random.random() < 0.5:
                image_tensor = transform.hflip(image_tensor)
                mask_tensor = transform.hflip(mask_tensor)
            if random.random() < 0.25:
                image_tensor = transform.adjust_brightness(
                    image_tensor, random.uniform(0.85, 1.15)
                )
            if random.random() < 0.25:
                image_tensor = transform.adjust_contrast(
                    image_tensor, random.uniform(0.85, 1.15)
                )

        image_tensor = transform.normalize(
            image_tensor,
            mean=(0.485, 0.456, 0.406),
            std=(0.229, 0.224, 0.225),
        )
        return image_tensor, mask_tensor
