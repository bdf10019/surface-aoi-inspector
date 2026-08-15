# AI v1 evaluation results

## Scope

The first AI baseline uses an LR-ASPP MobileNetV3-Large segmentation model. It was trained only with the official KSDD2 `train` split. A deterministic, class-stratified 15% subset was reserved for model selection, and the official `test` split remained untouched until the checkpoint had been selected.

This is an engineering baseline for comparison and integration work. It is not a production acceptance claim.

## Reproducible configuration

- Random seed: 42
- Training images: 1,982
- Validation images: 350
- Official test images: 1,004 (110 defective, 894 normal)
- Input size: 256 × 640 RGB
- Optimizer: AdamW, learning rate 0.001, weight decay 0.0001
- Loss: weighted binary cross-entropy plus Dice loss
- Training: 5 epochs with class-balanced image sampling
- Hardware: NVIDIA GeForce RTX 5060 Laptop GPU
- Selected checkpoint: epoch 2, chosen by validation balanced accuracy
- Decision rule: pixel probability ≥ 0.5 and predicted area ≥ 0.05% of the image

## Results

| Metric | Validation | Official test |
|---|---:|---:|
| Accuracy | 96.57% | 95.02% |
| Precision | 79.07% | 72.73% |
| Defect recall | 91.89% | 87.27% |
| Specificity | 97.12% | 95.97% |
| Balanced accuracy | 94.51% | 91.62% |
| Pixel Dice | 77.21% | 73.38% |

ONNX Runtime CPU inference was measured over 99 images after one warm-up image:

- Median: 9.85 ms/image
- Mean: 10.03 ms/image
- 95th percentile: 11.67 ms/image

The timing covers model inference only. Image decoding, resizing, normalization, post-processing, and UI rendering are excluded.

## Comparison with the traditional baseline

| Metric | Traditional detector | AI v1 |
|---|---:|---:|
| Accuracy | 91.83% | 95.02% |
| Precision | 64.00% | 72.73% |
| Defect recall | 58.18% | 87.27% |
| Specificity | 95.97% | 95.97% |

AI v1 improves defect recall by 29.09 percentage points while retaining the same measured specificity. The traditional detector remains much faster at roughly 0.90 ms of core processing per image, although the two timing measurements do not include identical pipeline stages.

## Limitations and next decision

- The dataset contains one material and one controlled acquisition setup; cross-product generalization has not been established.
- Epochs 3 and 4 were unstable, so the checkpoint must remain selected by validation metrics rather than final training loss.
- The image-level threshold has not been calibrated for a specific production cost of false negatives versus false positives.
- CPU timing was measured on one machine and is not a deployment guarantee.
- Repeated tuning must use training and validation data, not the official test split.

The selected ONNX model was therefore integrated into the desktop application behind configurable AI, traditional, and comparison modes. The deployed pipeline measurement is recorded below.

## Desktop C# deployment validation

The selected model was subsequently integrated with Microsoft ONNX Runtime 1.28.0. The deployed C# pipeline was run over all 1,004 official test images using the same probability and area thresholds.

| Metric | Python reference | C# desktop pipeline |
|---|---:|---:|
| Accuracy | 95.02% | 97.11% |
| Precision | 72.73% | 88.57% |
| Defect recall | 87.27% | 84.55% |
| Specificity | 95.97% | 98.66% |
| Balanced accuracy | 91.62% | 91.60% |

The C# confusion matrix is TP 93, TN 882, FP 12, and FN 17. The small operating-point shift is caused by the deployed bilinear resize differing from Torchvision's antialiased resize. Balanced accuracy differs by only 0.02 percentage points, but deployment acceptance should use the C# figures because they represent the executable application.

The mean C# ONNX inference time was 9.16 ms per image, with an 8.95 ms median and 10.43 ms 95th percentile. Full dataset processing, including file decoding, preprocessing, post-processing, and progress reporting, took 33.81 seconds.
