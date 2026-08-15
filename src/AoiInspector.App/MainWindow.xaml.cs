using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AoiInspector.Core;
using Microsoft.Win32;

namespace AoiInspector.App;

public partial class MainWindow : Window
{
    private readonly SurfaceDefectInspector _inspector = new();
    private GrayImage? _currentImage;

    public MainWindow()
    {
        InitializeComponent();
        LoadDemoImage();
    }

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
            _currentImage = ToGrayImage(bitmap);
            OriginalImage.Source = bitmap;
            MaskImage.Source = null;
            SetReady($"已載入 {bitmap.PixelWidth} × {bitmap.PixelHeight} 影像");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void CreateDemo_Click(object sender, RoutedEventArgs e) => LoadDemoImage();

    private void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage is null)
        {
            ShowError("請先載入一張影像。");
            return;
        }

        try
        {
            var settings = new InspectionSettings
            {
                BlurRadius = ParseInt(BlurRadiusTextBox.Text, "局部視窗半徑"),
                DifferenceThreshold = ParseInt(ThresholdTextBox.Text, "差異門檻"),
                MinimumDefectArea = ParseInt(MinimumAreaTextBox.Text, "最小瑕疵面積"),
                RejectAreaRatio = ParseDouble(RejectRatioTextBox.Text, "拒收面積比例") / 100d
            };

            var result = _inspector.Inspect(_currentImage, settings);
            MaskImage.Source = CreateMaskBitmap(result.DefectMask, _currentImage.Width, _currentImage.Height);
            StatusText.Text = result.IsPass ? "OK" : "NG";
            StatusText.Foreground = result.IsPass ? Brushes.LimeGreen : Brushes.OrangeRed;
            DetailText.Text = $"瑕疵區域：{result.DefectRegionCount}　瑕疵像素：{result.DefectPixelCount:N0}　面積比例：{result.DefectAreaRatio:P3}";
            TimingText.Text = $"處理時間：{result.ElapsedMilliseconds:F2} ms";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void LoadDemoImage()
    {
        const int width = 420;
        const int height = 300;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            pixels[(y * width) + x] = (byte)(154 + (((x * 17) + (y * 11)) % 9) - 4);

        for (var index = 0; index < 145; index++)
        {
            var centerX = 92 + index;
            var centerY = 76 + (index / 3);
            for (var offset = -2; offset <= 2; offset++)
                pixels[((centerY + offset) * width) + centerX] = 38;
        }

        _currentImage = new GrayImage(width, height, pixels);
        OriginalImage.Source = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
        MaskImage.Source = null;
        SetReady("已建立示範表面與刮痕");
    }

    private static GrayImage ToGrayImage(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight];
        converted.CopyPixels(pixels, converted.PixelWidth, 0);
        return new GrayImage(converted.PixelWidth, converted.PixelHeight, pixels);
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
}
