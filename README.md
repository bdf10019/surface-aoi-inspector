# Surface AOI Inspector

授權狀態：現行原創程式未另行授予通用開源授權；歷史版本與第三方／資料授權另見 [LICENSE_INFORMATION.md](LICENSE_INFORMATION.md)。

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
- 桌面介面支援傳統、AI、比較與融合判定四種模式
- AI 推論在背景執行，模型缺失時提供明確提示
- JSON 檢測配方可保存相機、光源、校正值與兩套演算法參數
- 依校正值將瑕疵遮罩換算為 mm² 與瑕疵中心座標
- 使用百分比 ROI 限定檢測區域，並保留相對整張影像的瑕疵座標
- 內建兩點尺度校正視窗，保存標定板／量具識別與校正時間
- 可選擇資料夾批次檢測，並輸出含判定原因與複核旗標的 CSV
- 批次紀錄包含批號、料號、配方版本／SHA-256、來源相對路徑、校正值及設備設定
- 保存來源影像、ONNX 模型／外部資料的 SHA-256，以及應用程式與演算法版本
- 可排除 `_GT`／mask／label 標註影像，並選擇是否掃描子資料夾
- 單張影像損壞時記錄 ERROR 後繼續整批，不讓單一輸入中止工作
- 批次錯誤、取消與人工複核總結由獨立工作流程核心測試
- 批次工作可安全停止；關閉視窗時會等待目前影像處理完成

## 專案結構

```text
src/AoiInspector.Core        檢測演算法與資料模型
src/AoiInspector.Ai          ONNX 前處理、推論與後處理
src/AoiInspector.Workflows   配方、融合規則、尺寸換算與 CSV
src/AoiInspector.App         Windows WPF 操作介面
tests/AoiInspector.SmokeTests 無外部相依的自動驗證
tests/AoiInspector.WorkflowTests 工作流程規則自動驗證
tools/AoiInspector.DatasetEvaluator 真實資料集批次評估工具
tools/AoiInspector.AiDatasetEvaluator C# ONNX 批次評估工具
ai                           AI 訓練、測試與 ONNX 匯出
models                       本機模型準備說明
data                         公開資料集放置位置
```

## 快速開始

需求：Windows 與 .NET 10 SDK。

```powershell
dotnet restore AoiInspector.slnx
dotnet build AoiInspector.slnx
dotnet run --project src/AoiInspector.App
```

GitHub 原始碼不包含公開資料集、訓練輸出或 ONNX 權重。全新下載的專案仍可直接啟動，使用傳統模式、示範影像、ROI、尺度校正、配方與批次追溯功能；選擇 AI 模式時會顯示模型缺失訊息。

## 啟用 AI

若本機已有經過驗證的 `ksdd2-v1` ONNX graph 與 external data：

```powershell
.\scripts\Prepare-AiModel.ps1
dotnet build AoiInspector.slnx
```

預設準備腳本只接受文件中記錄的模型 SHA-256，避免不明權重被誤當成已驗證版本。若要從公開資料集自行訓練新的模型，請先依 [`ai/README.md`](ai/README.md) 建立環境、下載資料並完成訓練；自行產生的權重不等同於本專案記錄的評估版本。

驗證核心演算法：

```powershell
dotnet run --project tests/AoiInspector.SmokeTests
dotnet run --project tests/AoiInspector.WorkflowTests -- recipes/precision-metal-starter.json
```

安裝 AI 模型後再執行：

```powershell
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

## 實務工作流程

程式內建「精密金屬表面－起始配方」，也提供可直接載入的 [`recipes/precision-metal-starter.json`](recipes/precision-metal-starter.json)。其中 `MicrometersPerPixel` 是展示用起始值，不是量測認證；實機使用前必須以標定板配合實際鏡頭、工作距離與影像解析度重新校正。完整操作與上線邊界見 [`docs/PRODUCTION_WORKFLOW.md`](docs/PRODUCTION_WORKFLOW.md)。

## 後續版本

1. 加入相機畸變校正、多點標定驗證與量測不確定度紀錄。
2. 抽象化工業相機觸發、PLC 握手與資料夾監看介面。
3. 製作可直接解壓執行的 Release 套件與展示素材。
