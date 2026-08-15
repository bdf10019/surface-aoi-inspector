# Surface AOI Inspector

一套在 Windows 執行的工業表面瑕疵檢測工作站。程式可載入產品影像、調整檢測參數、標記局部異常區域，並輸出 OK／NG、瑕疵面積與處理時間。

本作品本身是一個獨立的工業視覺專案，不依附任何公司或徵才內容。

## 目前功能

- 載入 PNG、JPG、BMP 與 TIFF 影像
- 內建可重現的表面刮痕示範影像
- 使用局部背景差異找出表面異常
- 移除小面積雜訊並計算缺陷區域
- 依瑕疵面積比例輸出 OK／NG
- 顯示像素級瑕疵遮罩與處理時間
- 核心檢測邏輯與桌面介面分離
- 可重現的 GPU 訓練、獨立測試與 ONNX 匯出流程
- 桌面介面支援傳統、AI 與比較三種檢測模式
- AI 推論在背景執行，模型缺失時提供明確提示

## 專案結構

```text
src/AoiInspector.Core        檢測演算法與資料模型
src/AoiInspector.Ai          ONNX 前處理、推論與後處理
src/AoiInspector.App         Windows WPF 操作介面
tests/AoiInspector.SmokeTests 無外部相依的自動驗證
tools/AoiInspector.DatasetEvaluator 真實資料集批次評估工具
tools/AoiInspector.AiDatasetEvaluator C# ONNX 批次評估工具
ai                           AI 訓練、測試與 ONNX 匯出
models                       本機模型準備說明
data                         公開資料集放置位置
```

## 執行

需求：Windows 與 .NET 10 SDK。

```powershell
.\scripts\Prepare-AiModel.ps1
dotnet build AoiInspector.slnx
dotnet run --project src/AoiInspector.App
```

若尚未準備 AI 模型，程式仍可使用傳統模式；選擇 AI 模式時會顯示模型缺失訊息。

驗證核心演算法：

```powershell
dotnet run --project tests/AoiInspector.SmokeTests
dotnet run --project tests/AoiInspector.AiSmokeTests
```

執行 KSDD2 測試集評估：

```powershell
dotnet run --project tools/AoiInspector.DatasetEvaluator -- data/raw/KolektorSDD2/test outputs/baseline-test-metrics.json
```

驗證桌面版實際使用的 C#／ONNX 流程：

```powershell
dotnet run --project tools/AoiInspector.AiDatasetEvaluator
```

## 公開資料集

正式驗證使用 [Kolektor Surface-Defect Dataset 2](https://www.vicos.si/resources/kolektorsdd2/)。資料由受控工業環境拍攝，包含正常與瑕疵影像，授權為 CC BY-NC-SA 4.0。

資料集不會直接提交到程式碼版本庫；下載後放入 `data/raw`，並保留原始授權與引用資訊。

目前傳統影像基線在 1,004 張官方測試影像上得到 91.83% 準確率、58.18% 瑕疵召回率，平均核心處理時間約 0.90 ms。完整結果與限制記錄在 [`docs/BASELINE_RESULTS.md`](docs/BASELINE_RESULTS.md)。這組結果是後續 AI 模型必須超越的基準，不代表已達到生產部署標準。

第一版 AI 模型在同一官方測試集上得到 95.02% 準確率、87.27% 瑕疵召回率與 73.38% 像素 Dice。匯出的 ONNX 模型在本機 CPU 上，中位純推論時間為 9.85 ms。完整設定、結果與限制記錄在 [`docs/AI_V1_RESULTS.md`](docs/AI_V1_RESULTS.md)。

桌面程式實際採用的 C#／ONNX Runtime 流程，在 1,004 張官方測試影像上得到 97.11% 準確率、84.55% 瑕疵召回率、88.57% 精確率與 91.60% 平衡準確率；完整整合方式記錄在 [`docs/DESKTOP_AI_INTEGRATION.md`](docs/DESKTOP_AI_INTEGRATION.md)。

## 後續版本

1. 加入檢測配方保存與載入。
2. 加入資料夾監看、連續檢測與 CSV 紀錄。
3. 製作可直接解壓執行的 Release 套件與展示素材。
