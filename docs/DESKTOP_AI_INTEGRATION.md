# Desktop AI integration

## Runtime design

The desktop application supports four modes:

- Traditional: local-background difference detector at the original image resolution.
- AI: LR-ASPP MobileNetV3 segmentation through ONNX Runtime CPU.
- Compare: both inspectors run on the same image and disagreements are flagged for review.
- Hybrid: both inspectors run and a recipe-selected rule produces the final verdict and review flag.

AI inspection runs on a worker thread so model loading and inference do not block the WPF interface. One `InferenceSession` is created lazily and reused until the window closes.

## AI pipeline

1. Decode the source as interleaved RGB bytes.
2. Bilinearly resize to 640 × 256.
3. Convert to NCHW floats and apply ImageNet mean and standard deviation.
4. Run `image` through the ONNX model and read `defect_logits`.
5. Apply sigmoid and the configurable pixel probability threshold.
6. Count mask pixels and connected regions.
7. Return OK when the predicted defect ratio is below the configured reject ratio.

Default settings match the Python evaluation rule: probability threshold 0.5 and reject area ratio 0.05%.

## Local model deployment

The model is not committed to Git. Run:

```powershell
.\scripts\Prepare-AiModel.ps1
```

The preparation script verifies both SHA-256 hashes, copies the ONNX graph and external data file to `models/local/ksdd2-v1`, and the WPF project copies them into `Models/ksdd2-v1` during build. Traditional mode remains available when the model is absent.

## Deployed pipeline verification

Run the actual C# preprocessing and ONNX inference implementation over the official test split:

```powershell
dotnet run --project tools/AoiInspector.AiDatasetEvaluator
```

Results on 1,004 images:

| Measurement | Result |
|---|---:|
| True positive | 93 |
| True negative | 882 |
| False positive | 12 |
| False negative | 17 |
| Accuracy | 97.11% |
| Precision | 88.57% |
| Defect recall | 84.55% |
| Specificity | 98.66% |
| Balanced accuracy | 91.60% |
| Median ONNX inference | 8.95 ms |

The C# result, rather than the Python reference result, is the acceptance baseline for future desktop changes.
