# Local model workspace

Trained model files are kept under `models/local` and excluded from Git. Prepare the selected local model before building the desktop application:

```powershell
.\scripts\Prepare-AiModel.ps1
```

The script copies both the ONNX graph and its external data file from `ai/runs/ksdd2-v1`, then verifies the selected model hashes. The application still runs in traditional mode when no AI model is installed and shows a clear message if AI mode is selected.

Selected model hashes:

- `surface_defect_segmenter.onnx`: `3B8AEE0FCEDE6BE7C470783582D34FA2F24C2ABB280D01DADF40466DBE6CABAA`
- `surface_defect_segmenter.onnx.data`: `935E5C97DE460554F16780FCEE504789FC7770ECCCC6F7DB5CD5BFAEA6CB288B`
