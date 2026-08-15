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

## 專案結構

```text
src/AoiInspector.Core        檢測演算法與資料模型
src/AoiInspector.App         Windows WPF 操作介面
tests/AoiInspector.SmokeTests 無外部相依的自動驗證
tools/AoiInspector.DatasetEvaluator 真實資料集批次評估工具
data                         公開資料集放置位置
```

## 執行

需求：Windows 與 .NET 10 SDK。

```powershell
dotnet build AoiInspector.slnx
dotnet run --project src/AoiInspector.App
```

驗證核心演算法：

```powershell
dotnet run --project tests/AoiInspector.SmokeTests
```

執行 KSDD2 測試集評估：

```powershell
dotnet run --project tools/AoiInspector.DatasetEvaluator -- data/raw/KolektorSDD2/test outputs/baseline-test-metrics.json
```

## 公開資料集

正式驗證預計使用 [Kolektor Surface-Defect Dataset 2](https://www.vicos.si/resources/kolektorsdd2/)。資料由受控工業環境拍攝，包含正常與瑕疵影像，授權為 CC BY-NC-SA 4.0。

資料集不會直接提交到程式碼版本庫；下載後放入 `data/raw`，並保留原始授權與引用資訊。

目前傳統影像基線在 1,004 張官方測試影像上得到 91.83% 準確率、58.18% 瑕疵召回率，平均核心處理時間約 0.90 ms。完整結果與限制記錄在 [`docs/BASELINE_RESULTS.md`](docs/BASELINE_RESULTS.md)。這組結果是後續 AI 模型必須超越的基準，不代表已達到生產部署標準。

## 後續版本

1. 加入 KSDD2 批次匯入、標註比對與統計報表。
2. 加入 OpenCV 影像前處理與檢測配方保存。
3. 加入 ONNX AI 模型推論，與傳統方法交叉判定。
4. 加入資料夾監看、連續檢測與 CSV 紀錄。
5. 建立可重現的效能與準確率測試報告。
