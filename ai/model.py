from __future__ import annotations

import torch
from torch import nn
from torchvision.models import MobileNet_V3_Large_Weights
from torchvision.models.segmentation import lraspp_mobilenet_v3_large


class SurfaceDefectSegmenter(nn.Module):
    def __init__(self, pretrained_backbone: bool = True) -> None:
        super().__init__()
        backbone_weights = (
            MobileNet_V3_Large_Weights.IMAGENET1K_V2 if pretrained_backbone else None
        )
        self.network = lraspp_mobilenet_v3_large(
            weights=None,
            weights_backbone=backbone_weights,
            num_classes=1,
        )

    def forward(self, images: torch.Tensor) -> torch.Tensor:
        return self.network(images)["out"]
