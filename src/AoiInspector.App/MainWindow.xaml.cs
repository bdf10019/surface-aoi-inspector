using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AoiInspector.Ai;
using AoiInspector.Core;
using AoiInspector.Workflows;
using Microsoft.Win32;

namespace AoiInspector.App;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];

    private readonly SurfaceDefectInspector _traditionalInspector = new();
    private OnnxSurfaceDefectInspector? _aiInspector;
    private RgbImage? _currentColorImage;
    private GrayImage? _currentGrayImage;
    private string _recipeDescription = string.Empty;
    private CancellationTokenSource? _operationCts;
    private bool _isRunning;
    private bool _closeRequested;

    public MainWindow()
    {
        InitializeComponent();
        ApplyRecipe(RecipeCatalog.BuiltIn[0]);
        LoadDemoImage();
        UpdateModelStatus();
    }

    private static string ModelPath => Path.Combine(
        AppContext.BaseDirectory, "Models", "ksdd2-v1",
        OnnxSurfaceDefectInspector.DefaultModelFileName);

    private void LoadImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "選擇檢測影像",
            Filter = "影像檔案|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有檔案|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var bitmap = LoadBitmap(dialog.FileName);
            _currentColorImage = ToRgbImage(bitmap);
            _currentGrayImage = _currentColorImage.ToGrayImage();
            OriginalImage.Source = bitmap;
            MaskImage.Source = null;
            ResetMaskHeading();
            SetReady($"已載入 {bitmap.PixelWidth} × {bitmap.PixelHeight} 影像");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void CreateDemo_Click(object sender, RoutedEventArgs e) => LoadDemoImage();

    private void ApplyBuiltInRecipe_Click(object sender, RoutedEventArgs e)
    {
        ApplyRecipe(RecipeCatalog.BuiltIn[0]);
        SetReady("已套用內建起始配方；上線前仍須完成設備校正。");
    }

    private void LoadRecipe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "載入檢測配方",
            Filter = "AOI 配方 (*.json)|*.json|所有檔案|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ApplyRecipe(RecipeFile.Load(dialog.FileName));
            SetReady($"已載入配方：{RecipeNameTextBox.Text}");
        }
        catch (Exception ex) { ShowError(ex.GetBaseException().Message); }
    }

    private void SaveRecipe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var recipe = ReadRecipeFromControls();
            var dialog = new SaveFileDialog
            {
                Title = "儲存檢測配方",
                Filter = "AOI 配方 (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = SanitizeFileName(recipe.Name) + ".json"
            };
            if (dialog.ShowDialog() != true) return;
            RecipeFile.Save(dialog.FileName, recipe);
            SetReady($"配方已儲存：{Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) { ShowError(ex.GetBaseException().Message); }
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (_currentColorImage is null || _currentGrayImage is null)
        {
            ShowError("請先載入一張影像。");
            return;
        }

        try
        {
            var recipe = ReadRecipeFromControls();
            BeginOperation("正在執行單張檢測…");
            var token = _operationCts!.Token;
            var execution = await Task.Run(
                () => ExecuteInspection(_currentColorImage, _currentGrayImage, recipe), token);
            token.ThrowIfCancellationRequested();
            ShowExecution(execution, recipe);
        }
        catch (OperationCanceledException) { ShowCancelled("單張檢測已停止。"); }
        catch (Exception ex) { ShowError(ex.GetBaseException().Message); }
        finally { EndOperation(); }
    }

    private async void BatchInspect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var recipe = ReadRecipeFromControls();
            var folderDialog = new OpenFolderDialog
            {
                Title = "選擇批次檢測資料夾",
                Multiselect = false
            };
            if (folderDialog.ShowDialog() != true) return;

            var files = Directory.EnumerateFiles(folderDialog.FolderName)
                .Where(path => SupportedImageExtensions.Contains(
                    Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
                throw new InvalidOperationException("選定資料夾中沒有支援的影像檔。");

            var saveDialog = new SaveFileDialog
            {
                Title = "儲存批次檢測紀錄",
                Filter = "CSV 紀錄 (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = $"inspection-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };
            if (saveDialog.ShowDialog() != true) return;

            BeginOperation($"準備批次檢測，共 {files.Length} 張…");
            var token = _operationCts!.Token;
            var entries = new List<InspectionLogEntry>();
            try
            {
                for (var index = 0; index < files.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    StatusText.Text = "BATCH";
                    DetailText.Text = $"{index + 1} / {files.Length}　{Path.GetFileName(files[index])}";
                    var path = files[index];
                    var execution = await Task.Run(() =>
                    {
                        var color = LoadRgbImage(path);
                        return ExecuteInspection(color, color.ToGrayImage(), recipe);
                    }, token);
                    token.ThrowIfCancellationRequested();
                    entries.Add(CreateLogEntry(path, recipe, execution));
                }

                InspectionCsv.Write(saveDialog.FileName, entries);
                var ng = entries.Count(entry => entry.Verdict == InspectionVerdict.Ng);
                var review = entries.Count(entry => entry.RequiresReview);
                StatusText.Text = ng == 0 ? "BATCH OK" : "BATCH DONE";
                StatusText.Foreground = ng == 0 ? Brushes.LimeGreen : Brushes.Gold;
                DetailText.Text = $"完成 {entries.Count} 張；NG {ng}、需複核 {review}。CSV：{Path.GetFileName(saveDialog.FileName)}";
                TimingText.Text = $"總核心時間：{entries.Sum(entry => entry.ElapsedMilliseconds):F1} ms";
            }
            catch (OperationCanceledException)
            {
                if (entries.Count > 0) InspectionCsv.Write(saveDialog.FileName, entries);
                ShowCancelled($"批次已停止；已完成 {entries.Count} 張並保留部分 CSV。");
            }
        }
        catch (Exception ex) { ShowError(ex.GetBaseException().Message); }
        finally { EndOperation(); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        DetailText.Text = "正在安全停止；目前影像完成後結束…";
        _operationCts?.Cancel();
    }

    private InspectionExecution ExecuteInspection(
        RgbImage colorImage, GrayImage grayImage, InspectionRecipe recipe)
    {
        InspectionResult? traditional = null;
        AiInspectionResult? ai = null;
        if (recipe.Mode is InspectionMode.Traditional or InspectionMode.Compare or InspectionMode.Hybrid)
            traditional = _traditionalInspector.Inspect(grayImage, recipe.TraditionalSettings);
        if (recipe.Mode is InspectionMode.Ai or InspectionMode.Compare or InspectionMode.Hybrid)
            ai = GetAiInspector().Inspect(colorImage, recipe.AiSettings);

        var decision = recipe.Mode switch
        {
            InspectionMode.Traditional => new FusionDecision(
                traditional!.IsPass ? InspectionVerdict.Ok : InspectionVerdict.Ng, false,
                traditional.IsPass ? "TRADITIONAL_OK" : "TRADITIONAL_NG"),
            InspectionMode.Ai => new FusionDecision(
                ai!.IsPass ? InspectionVerdict.Ok : InspectionVerdict.Ng, false,
                ai.IsPass ? "AI_OK" : "AI_NG"),
            InspectionMode.Compare => DecisionFusion.Evaluate(
                traditional!.IsPass, ai!.IsPass, HybridDecisionRule.Consensus),
            InspectionMode.Hybrid => DecisionFusion.Evaluate(
                traditional!.IsPass, ai!.IsPass, recipe.HybridRule),
            _ => throw new ArgumentOutOfRangeException()
        };

        var traditionalMeasurement = traditional is null ? null : MaskMeasurement.Measure(
            traditional.DefectMask, grayImage.Width, grayImage.Height,
            grayImage.Width, grayImage.Height, recipe.MicrometersPerPixel);
        var aiMeasurement = ai is null ? null : MaskMeasurement.Measure(
            ai.DefectMask, ai.MaskWidth, ai.MaskHeight,
            grayImage.Width, grayImage.Height, recipe.MicrometersPerPixel);
        return new InspectionExecution(traditional, ai, decision,
            traditionalMeasurement, aiMeasurement, grayImage.Width, grayImage.Height);
    }

    private static InspectionLogEntry CreateLogEntry(
        string path, InspectionRecipe recipe, InspectionExecution execution) => new(
            DateTimeOffset.Now, Path.GetFileName(path), recipe.Name, recipe.Mode,
            execution.Decision.Verdict, execution.Decision.RequiresReview,
            execution.Decision.ReasonCode, execution.Traditional?.DefectAreaRatio,
            execution.Ai?.DefectAreaRatio,
            execution.TraditionalMeasurement?.DefectAreaSquareMillimeters,
            execution.AiMeasurement?.DefectAreaSquareMillimeters,
            (execution.Traditional?.ElapsedMilliseconds ?? 0) +
            (execution.Ai?.ElapsedMilliseconds ?? 0));

    private void ShowExecution(InspectionExecution execution, InspectionRecipe recipe)
    {
        SetVerdict(execution.Decision);
        switch (recipe.Mode)
        {
            case InspectionMode.Traditional: ShowTraditionalResult(execution); break;
            case InspectionMode.Ai: ShowAiResult(execution); break;
            case InspectionMode.Compare:
            case InspectionMode.Hybrid:
                ShowCombinedResult(execution, recipe.Mode == InspectionMode.Hybrid); break;
        }
    }

    private void ShowTraditionalResult(InspectionExecution execution)
    {
        var result = execution.Traditional!;
        MaskImage.Source = CreateMaskBitmap(result.DefectMask, execution.SourceWidth, execution.SourceHeight);
        MaskTitleText.Text = "傳統瑕疵遮罩"; MaskLegendText.Text = "紅色：局部差異";
        DetailText.Text = $"區域：{result.DefectRegionCount}　比例：{result.DefectAreaRatio:P3}　" +
            MeasurementText(execution.TraditionalMeasurement!);
        TimingText.Text = $"傳統核心：{result.ElapsedMilliseconds:F2} ms";
    }

    private void ShowAiResult(InspectionExecution execution)
    {
        var result = execution.Ai!;
        MaskImage.Source = CreateMaskBitmap(result.DefectMask, result.MaskWidth, result.MaskHeight);
        MaskTitleText.Text = "AI 瑕疵遮罩"; MaskLegendText.Text = "紅色：模型預測";
        DetailText.Text = $"區域：{result.DefectRegionCount}　比例：{result.DefectAreaRatio:P3}　" +
            MeasurementText(execution.AiMeasurement!);
        TimingText.Text = $"AI 總計：{result.ElapsedMilliseconds:F2} ms（推論 {result.InferenceMilliseconds:F2}）";
    }

    private void ShowCombinedResult(InspectionExecution execution, bool isHybrid)
    {
        var traditional = execution.Traditional!; var ai = execution.Ai!;
        MaskImage.Source = CreateComparisonMask(
            traditional.DefectMask, execution.SourceWidth, execution.SourceHeight,
            ai.DefectMask, ai.MaskWidth, ai.MaskHeight);
        MaskTitleText.Text = isHybrid ? "融合判定遮罩" : "比較遮罩";
        MaskLegendText.Text = "橘：傳統　青：AI　紫：重疊";
        DetailText.Text =
            $"傳統：{Verdict(traditional.IsPass)}／{traditional.DefectAreaRatio:P3}／{execution.TraditionalMeasurement!.DefectAreaSquareMillimeters:F4} mm²　" +
            $"AI：{Verdict(ai.IsPass)}／{ai.DefectAreaRatio:P3}／{execution.AiMeasurement!.DefectAreaSquareMillimeters:F4} mm²　" +
            $"原因：{execution.Decision.ReasonCode}";
        TimingText.Text = $"傳統 {traditional.ElapsedMilliseconds:F2} ms　AI {ai.ElapsedMilliseconds:F2} ms";
    }

    private InspectionRecipe ReadRecipeFromControls()
    {
        var recipe = new InspectionRecipe
        {
            Name = RecipeNameTextBox.Text.Trim(),
            Description = _recipeDescription,
            Mode = (InspectionMode)InspectionModeComboBox.SelectedIndex,
            HybridRule = (HybridDecisionRule)HybridRuleComboBox.SelectedIndex,
            MicrometersPerPixel = ParseDouble(MicrometersPerPixelTextBox.Text, "像素尺寸"),
            CameraProfile = CameraProfileTextBox.Text.Trim(),
            LightingProfile = LightingProfileTextBox.Text.Trim(),
            TraditionalSettings = ParseTraditionalSettings(),
            AiSettings = ParseAiSettings()
        };
        recipe.Validate();
        return recipe;
    }

    private void ApplyRecipe(InspectionRecipe recipe)
    {
        recipe.Validate(); _recipeDescription = recipe.Description;
        RecipeNameTextBox.Text = recipe.Name;
        InspectionModeComboBox.SelectedIndex = (int)recipe.Mode;
        HybridRuleComboBox.SelectedIndex = (int)recipe.HybridRule;
        MicrometersPerPixelTextBox.Text = recipe.MicrometersPerPixel.ToString("0.####", CultureInfo.InvariantCulture);
        CameraProfileTextBox.Text = recipe.CameraProfile; LightingProfileTextBox.Text = recipe.LightingProfile;
        BlurRadiusTextBox.Text = recipe.TraditionalSettings.BlurRadius.ToString(CultureInfo.InvariantCulture);
        ThresholdTextBox.Text = recipe.TraditionalSettings.DifferenceThreshold.ToString(CultureInfo.InvariantCulture);
        MinimumAreaTextBox.Text = recipe.TraditionalSettings.MinimumDefectArea.ToString(CultureInfo.InvariantCulture);
        RejectRatioTextBox.Text = (recipe.TraditionalSettings.RejectAreaRatio * 100).ToString("0.####", CultureInfo.InvariantCulture);
        AiProbabilityTextBox.Text = recipe.AiSettings.PixelProbabilityThreshold.ToString("0.####", CultureInfo.InvariantCulture);
        AiRejectRatioTextBox.Text = (recipe.AiSettings.RejectAreaRatio * 100).ToString("0.####", CultureInfo.InvariantCulture);
    }

    private InspectionSettings ParseTraditionalSettings() => new()
    {
        BlurRadius = ParseInt(BlurRadiusTextBox.Text, "局部視窗半徑"),
        DifferenceThreshold = ParseInt(ThresholdTextBox.Text, "差異門檻"),
        MinimumDefectArea = ParseInt(MinimumAreaTextBox.Text, "最小瑕疵面積"),
        RejectAreaRatio = ParseDouble(RejectRatioTextBox.Text, "傳統拒收比例") / 100d
    };

    private AiInspectionSettings ParseAiSettings() => new()
    {
        PixelProbabilityThreshold = (float)ParseDouble(AiProbabilityTextBox.Text, "AI 像素機率門檻"),
        RejectAreaRatio = ParseDouble(AiRejectRatioTextBox.Text, "AI 拒收比例") / 100d
    };

    private OnnxSurfaceDefectInspector GetAiInspector() =>
        _aiInspector ??= new OnnxSurfaceDefectInspector(ModelPath);

    private void LoadDemoImage()
    {
        const int width = 420; const int height = 300;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = (byte)(154 + (((x * 17) + (y * 11)) % 9) - 4);
                var index = ((y * width) + x) * 3;
                rgb[index] = value; rgb[index + 1] = value; rgb[index + 2] = value;
            }
        for (var index = 0; index < 145; index++)
        {
            var centerX = 92 + index; var centerY = 76 + (index / 3);
            for (var offset = -2; offset <= 2; offset++)
            {
                var target = (((centerY + offset) * width) + centerX) * 3;
                rgb[target] = rgb[target + 1] = rgb[target + 2] = 38;
            }
        }
        _currentColorImage = new RgbImage(width, height, rgb);
        _currentGrayImage = _currentColorImage.ToGrayImage();
        OriginalImage.Source = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Rgb24, null, rgb, width * 3);
        MaskImage.Source = null; ResetMaskHeading(); SetReady("已建立示範表面與刮痕");
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path);
        bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }

    private static RgbImage LoadRgbImage(string path) => ToRgbImage(LoadBitmap(path));

    private static RgbImage ToRgbImage(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4; var bgra = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(bgra, stride, 0);
        var rgb = new byte[converted.PixelWidth * converted.PixelHeight * 3];
        for (var index = 0; index < converted.PixelWidth * converted.PixelHeight; index++)
        {
            var sourceIndex = index * 4; var targetIndex = index * 3;
            rgb[targetIndex] = bgra[sourceIndex + 2]; rgb[targetIndex + 1] = bgra[sourceIndex + 1];
            rgb[targetIndex + 2] = bgra[sourceIndex];
        }
        return new RgbImage(converted.PixelWidth, converted.PixelHeight, rgb);
    }

    private static BitmapSource CreateMaskBitmap(byte[] mask, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < mask.Length; index++)
        {
            var target = index * 4;
            if (mask[index] != 0) { pixels[target] = 45; pixels[target + 1] = 70; pixels[target + 2] = 255; }
            else { pixels[target] = 24; pixels[target + 1] = 20; pixels[target + 2] = 16; }
            pixels[target + 3] = 255;
        }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    private static BitmapSource CreateComparisonMask(
        byte[] traditional, int traditionalWidth, int traditionalHeight,
        byte[] ai, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var targetIndex = (y * width) + x;
                var traditionalX = Math.Min(traditionalWidth - 1, x * traditionalWidth / width);
                var traditionalY = Math.Min(traditionalHeight - 1, y * traditionalHeight / height);
                var traditionalHit = traditional[(traditionalY * traditionalWidth) + traditionalX] != 0;
                var aiHit = ai[targetIndex] != 0; var pixel = targetIndex * 4;
                if (traditionalHit && aiHit) { pixels[pixel] = 235; pixels[pixel + 1] = 70; pixels[pixel + 2] = 210; }
                else if (traditionalHit) { pixels[pixel] = 30; pixels[pixel + 1] = 145; pixels[pixel + 2] = 255; }
                else if (aiHit) { pixels[pixel] = 220; pixels[pixel + 1] = 210; pixels[pixel + 2] = 35; }
                else { pixels[pixel] = 24; pixels[pixel + 1] = 20; pixels[pixel + 2] = 16; }
                pixels[pixel + 3] = 255;
            }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"{name}必須是整數。");
        return parsed;
    }

    private static double ParseDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"{name}必須是數字，請使用小數點。");
        return parsed;
    }

    private static string MeasurementText(PhysicalMeasurement measurement)
    {
        var centroid = measurement.CentroidXMillimeters.HasValue
            ? $"中心：({measurement.CentroidXMillimeters:F3}, {measurement.CentroidYMillimeters:F3}) mm" : "中心：—";
        return $"面積：{measurement.DefectAreaSquareMillimeters:F4} mm²　{centroid}";
    }

    private void SetVerdict(FusionDecision decision)
    {
        StatusText.Text = decision.Verdict switch
        {
            InspectionVerdict.Ok => "OK",
            InspectionVerdict.Ng => decision.RequiresReview ? "NG／複核" : "NG",
            InspectionVerdict.Review => "需複核",
            _ => "UNKNOWN"
        };
        StatusText.Foreground = decision.Verdict switch
        {
            InspectionVerdict.Ok => Brushes.LimeGreen,
            InspectionVerdict.Ng => Brushes.OrangeRed,
            _ => Brushes.Gold
        };
    }

    private static string Verdict(bool isPass) => isPass ? "OK" : "NG";

    private void BeginOperation(string message)
    {
        _operationCts?.Dispose(); _operationCts = new CancellationTokenSource(); _isRunning = true;
        InspectButton.IsEnabled = false; BatchButton.IsEnabled = false; CancelButton.IsEnabled = true;
        StatusText.Text = "RUNNING"; StatusText.Foreground = Brushes.DeepSkyBlue;
        DetailText.Text = message; TimingText.Text = "處理時間：—";
    }

    private void EndOperation()
    {
        _isRunning = false; _operationCts?.Dispose(); _operationCts = null;
        InspectButton.IsEnabled = true; BatchButton.IsEnabled = true; CancelButton.IsEnabled = false;
        if (_closeRequested) Dispatcher.BeginInvoke(Close);
    }

    private void SetReady(string message)
    {
        StatusText.Text = "READY"; StatusText.Foreground = Brushes.DeepSkyBlue;
        DetailText.Text = message; TimingText.Text = "處理時間：—";
    }

    private void ShowCancelled(string message)
    {
        StatusText.Text = "STOPPED"; StatusText.Foreground = Brushes.Gold;
        DetailText.Text = message; TimingText.Text = "處理時間：—";
    }

    private void ShowError(string message)
    {
        StatusText.Text = "ERROR"; StatusText.Foreground = Brushes.Gold;
        DetailText.Text = message; TimingText.Text = "處理時間：—";
    }

    private void ResetMaskHeading() { MaskTitleText.Text = "瑕疵遮罩"; MaskLegendText.Text = string.Empty; }

    private void UpdateModelStatus()
    {
        var available = File.Exists(ModelPath) && File.Exists(ModelPath + ".data");
        ModelStatusText.Text = available ? "AI 模型：已安裝（CPU）" : "AI 模型：尚未安裝";
        ModelStatusText.Foreground = available ? Brushes.LightGreen : Brushes.Gold;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var character in Path.GetInvalidFileNameChars()) name = name.Replace(character, '-');
        return string.IsNullOrWhiteSpace(name) ? "inspection-recipe" : name;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isRunning)
        {
            e.Cancel = true; _closeRequested = true; _operationCts?.Cancel();
            DetailText.Text = "正在安全停止作業後關閉…"; return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _aiInspector?.Dispose(); _operationCts?.Dispose(); base.OnClosed(e);
    }

    private sealed record InspectionExecution(
        InspectionResult? Traditional, AiInspectionResult? Ai, FusionDecision Decision,
        PhysicalMeasurement? TraditionalMeasurement, PhysicalMeasurement? AiMeasurement,
        int SourceWidth, int SourceHeight);
}
