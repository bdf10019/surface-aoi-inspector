using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AoiInspector.Ai;
using AoiInspector.Core;
using Microsoft.Win32;

namespace AoiInspector.App;

public partial class MainWindow : Window
{
    private readonly SurfaceDefectInspector _traditionalInspector = new();
    private OnnxSurfaceDefectInspector? _aiInspector;
    private RgbImage? _currentColorImage;
    private GrayImage? _currentGrayImage;

    public MainWindow()
    {
        InitializeComponent();
        LoadDemoImage();
        UpdateModelStatus();
    }

    private static string ModelPath => Path.Combine(
        AppContext.BaseDirectory,
        "Models",
        "ksdd2-v1",
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
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(dialog.FileName);
            bitmap.EndInit();
            bitmap.Freeze();
            _currentColorImage = ToRgbImage(bitmap);
            _currentGrayImage = _currentColorImage.ToGrayImage();
            OriginalImage.Source = bitmap;
            MaskImage.Source = null;
            ResetMaskHeading();
            SetReady($"已載入 {bitmap.PixelWidth} × {bitmap.PixelHeight} 影像");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void CreateDemo_Click(object sender, RoutedEventArgs e) => LoadDemoImage();

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (_currentColorImage is null || _currentGrayImage is null)
        {
            ShowError("請先載入一張影像。");
            return;
        }

        InspectButton.IsEnabled = false;
        StatusText.Text = "RUNNING";
        StatusText.Foreground = Brushes.DeepSkyBlue;
        DetailText.Text = "正在執行檢測…";
        TimingText.Text = "處理時間：—";

        try
        {
            var mode = (InspectionMode)InspectionModeComboBox.SelectedIndex;
            var traditionalSettings = mode is InspectionMode.Traditional or InspectionMode.Compare
                ? ParseTraditionalSettings()
                : null;
            var aiSettings = mode is InspectionMode.Ai or InspectionMode.Compare
                ? ParseAiSettings()
                : null;
            var colorImage = _currentColorImage;
            var grayImage = _currentGrayImage;

            var results = await Task.Run(() =>
            {
                InspectionResult? traditional = null;
                AiInspectionResult? ai = null;

                if (mode is InspectionMode.Traditional or InspectionMode.Compare)
                    traditional = _traditionalInspector.Inspect(grayImage, traditionalSettings!);
                if (mode is InspectionMode.Ai or InspectionMode.Compare)
                    ai = GetAiInspector().Inspect(colorImage, aiSettings!);

                return (Traditional: traditional, Ai: ai);
            });

            switch (mode)
            {
                case InspectionMode.Traditional:
                    ShowTraditionalResult(results.Traditional!, grayImage.Width, grayImage.Height);
                    break;
                case InspectionMode.Ai:
                    ShowAiResult(results.Ai!);
                    break;
                case InspectionMode.Compare:
                    ShowComparisonResult(results.Traditional!, results.Ai!, grayImage.Width, grayImage.Height);
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.GetBaseException().Message);
        }
        finally
        {
            InspectButton.IsEnabled = true;
        }
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

    private void ShowTraditionalResult(InspectionResult result, int width, int height)
    {
        MaskImage.Source = CreateMaskBitmap(result.DefectMask, width, height);
        MaskTitleText.Text = "傳統瑕疵遮罩";
        MaskLegendText.Text = "紅色：局部差異";
        SetVerdict(result.IsPass);
        DetailText.Text = $"區域：{result.DefectRegionCount}　像素：{result.DefectPixelCount:N0}　比例：{result.DefectAreaRatio:P3}";
        TimingText.Text = $"傳統核心：{result.ElapsedMilliseconds:F2} ms";
    }

    private void ShowAiResult(AiInspectionResult result)
    {
        MaskImage.Source = CreateMaskBitmap(result.DefectMask, result.MaskWidth, result.MaskHeight);
        MaskTitleText.Text = "AI 瑕疵遮罩";
        MaskLegendText.Text = "紅色：模型預測";
        SetVerdict(result.IsPass);
        DetailText.Text = $"區域：{result.DefectRegionCount}　像素：{result.DefectPixelCount:N0}　比例：{result.DefectAreaRatio:P3}";
        TimingText.Text = $"AI 總計：{result.ElapsedMilliseconds:F2} ms（推論 {result.InferenceMilliseconds:F2}）";
    }

    private void ShowComparisonResult(
        InspectionResult traditional,
        AiInspectionResult ai,
        int originalWidth,
        int originalHeight)
    {
        MaskImage.Source = CreateComparisonMask(
            traditional.DefectMask, originalWidth, originalHeight,
            ai.DefectMask, ai.MaskWidth, ai.MaskHeight);
        MaskTitleText.Text = "比較遮罩";
        MaskLegendText.Text = "橘：傳統　青：AI　紫：重疊";

        if (traditional.IsPass == ai.IsPass)
        {
            StatusText.Text = ai.IsPass ? "一致 OK" : "一致 NG";
            StatusText.Foreground = ai.IsPass ? Brushes.LimeGreen : Brushes.OrangeRed;
        }
        else
        {
            StatusText.Text = "需複核";
            StatusText.Foreground = Brushes.Gold;
        }

        DetailText.Text =
            $"傳統：{Verdict(traditional.IsPass)}／{traditional.DefectAreaRatio:P3}　" +
            $"AI：{Verdict(ai.IsPass)}／{ai.DefectAreaRatio:P3}";
        TimingText.Text =
            $"傳統 {traditional.ElapsedMilliseconds:F2} ms　AI {ai.ElapsedMilliseconds:F2} ms";
    }

    private void LoadDemoImage()
    {
        const int width = 420;
        const int height = 300;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var value = (byte)(154 + (((x * 17) + (y * 11)) % 9) - 4);
            var index = ((y * width) + x) * 3;
            rgb[index] = value;
            rgb[index + 1] = value;
            rgb[index + 2] = value;
        }

        for (var index = 0; index < 145; index++)
        {
            var centerX = 92 + index;
            var centerY = 76 + (index / 3);
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
        MaskImage.Source = null;
        ResetMaskHeading();
        SetReady("已建立示範表面與刮痕");
    }

    private static RgbImage ToRgbImage(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var bgra = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(bgra, stride, 0);
        var rgb = new byte[converted.PixelWidth * converted.PixelHeight * 3];

        for (var index = 0; index < converted.PixelWidth * converted.PixelHeight; index++)
        {
            var sourceIndex = index * 4;
            var targetIndex = index * 3;
            rgb[targetIndex] = bgra[sourceIndex + 2];
            rgb[targetIndex + 1] = bgra[sourceIndex + 1];
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
            if (mask[index] != 0)
            {
                pixels[target] = 45;
                pixels[target + 1] = 70;
                pixels[target + 2] = 255;
            }
            else
            {
                pixels[target] = 24;
                pixels[target + 1] = 20;
                pixels[target + 2] = 16;
            }
            pixels[target + 3] = 255;
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    private static BitmapSource CreateComparisonMask(
        byte[] traditional,
        int traditionalWidth,
        int traditionalHeight,
        byte[] ai,
        int width,
        int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var targetIndex = (y * width) + x;
            var traditionalX = Math.Min(traditionalWidth - 1, x * traditionalWidth / width);
            var traditionalY = Math.Min(traditionalHeight - 1, y * traditionalHeight / height);
            var traditionalHit = traditional[(traditionalY * traditionalWidth) + traditionalX] != 0;
            var aiHit = ai[targetIndex] != 0;
            var pixel = targetIndex * 4;

            if (traditionalHit && aiHit)
            {
                pixels[pixel] = 235;
                pixels[pixel + 1] = 70;
                pixels[pixel + 2] = 210;
            }
            else if (traditionalHit)
            {
                pixels[pixel] = 30;
                pixels[pixel + 1] = 145;
                pixels[pixel + 2] = 255;
            }
            else if (aiHit)
            {
                pixels[pixel] = 220;
                pixels[pixel + 1] = 210;
                pixels[pixel + 2] = 35;
            }
            else
            {
                pixels[pixel] = 24;
                pixels[pixel + 1] = 20;
                pixels[pixel + 2] = 16;
            }

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

    private void SetVerdict(bool isPass)
    {
        StatusText.Text = Verdict(isPass);
        StatusText.Foreground = isPass ? Brushes.LimeGreen : Brushes.OrangeRed;
    }

    private static string Verdict(bool isPass) => isPass ? "OK" : "NG";

    private void SetReady(string message)
    {
        StatusText.Text = "READY";
        StatusText.Foreground = Brushes.DeepSkyBlue;
        DetailText.Text = message;
        TimingText.Text = "處理時間：—";
    }

    private void ShowError(string message)
    {
        StatusText.Text = "ERROR";
        StatusText.Foreground = Brushes.Gold;
        DetailText.Text = message;
        TimingText.Text = "處理時間：—";
    }

    private void ResetMaskHeading()
    {
        MaskTitleText.Text = "瑕疵遮罩";
        MaskLegendText.Text = string.Empty;
    }

    private void UpdateModelStatus()
    {
        var available = File.Exists(ModelPath) && File.Exists(ModelPath + ".data");
        ModelStatusText.Text = available ? "AI 模型：已安裝（CPU）" : "AI 模型：尚未安裝";
        ModelStatusText.Foreground = available ? Brushes.LightGreen : Brushes.Gold;
    }

    protected override void OnClosed(EventArgs e)
    {
        _aiInspector?.Dispose();
        base.OnClosed(e);
    }

    private enum InspectionMode
    {
        Traditional,
        Ai,
        Compare
    }
}
