# AI training pipeline

This pipeline trains a lightweight LR-ASPP MobileNetV3 surface-defect segmentation model on the KSDD2 training split, validates on a deterministic stratified subset, and exports the best checkpoint to ONNX.

## Environment

- Python 3.12
- PyTorch 2.13.0 + CUDA 13.0
- Torchvision 0.28.0
- ONNX 1.22.0
- ONNX Runtime 1.28.0

The isolated environment lives in `.venv` and is excluded from Git.

Create the environment from a fresh clone:

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r ai\requirements-lock.txt
.\scripts\Get-KSDD2.ps1
```

## Train

```powershell
.\.venv\Scripts\python.exe ai\train.py --epochs 5 --output-dir ai\runs\ksdd2-v1
```

Run a fast end-to-end smoke test:

```powershell
.\.venv\Scripts\python.exe ai\train.py --epochs 1 --max-train-samples 32 --max-validation-samples 16 --output-dir ai\runs\smoke
```

Generated checkpoints, metrics, and ONNX files remain local under `ai/runs` until a model has been fully evaluated and selected for release.

Evaluate the selected checkpoint once on the untouched official test split:

```powershell
.\.venv\Scripts\python.exe ai\evaluate.py --run-dir ai\runs\ksdd2-v1
```

This writes `test_metrics.json` beside the checkpoint and also measures ONNX Runtime CPU latency. Do not use the test result to repeatedly tune model settings; return to the training/validation split for tuning.

## Install a locally trained model

The selected evaluation model is not redistributed in this repository. A newly trained model will normally have different hashes and must not be represented as the documented `ksdd2-v1` artifact. To explicitly install your own locally produced graph and external data for application testing:

```powershell
.\scripts\Prepare-AiModel.ps1 `
  -SourceDirectory ai\runs\ksdd2-v1 `
  -AllowUnverifiedArtifacts
dotnet build AoiInspector.slnx
dotnet run --project tests/AoiInspector.AiSmokeTests
```

`-AllowUnverifiedArtifacts` only bypasses comparison with the selected model hashes. The script still records and prints the SHA-256 of both copied files; evaluate the resulting C# pipeline before making any performance claim.
