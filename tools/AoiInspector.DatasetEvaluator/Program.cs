using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AoiInspector.Core;

if (args.Length == 0 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: AoiInspector.DatasetEvaluator <KSDD2 train-or-test-folder> [output-json]");
    return 2;
}

var datasetPath = Path.GetFullPath(args[0]);
var outputPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var images = Directory.EnumerateFiles(datasetPath, "*.png", SearchOption.TopDirectoryOnly)
    .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith("_GT", StringComparison.OrdinalIgnoreCase))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

var inspector = new SurfaceDefectInspector();
var settings = new InspectionSettings
{
    BlurRadius = 8,
    DifferenceThreshold = 28,
    MinimumDefectArea = 10,
    RejectAreaRatio = 0.0005
};

var truePositive = 0;
var trueNegative = 0;
var falsePositive = 0;
var falseNegative = 0;
double totalInspectionMs = 0;
var wallClock = Stopwatch.StartNew();

for (var index = 0; index < images.Length; index++)
{
    var imagePath = images[index];
    var maskPath = Path.Combine(
        Path.GetDirectoryName(imagePath)!,
        $"{Path.GetFileNameWithoutExtension(imagePath)}_GT.png");
    var image = LoadGray(imagePath);
    var truthMask = LoadGray(maskPath);
    var expectedDefect = truthMask.Pixels.Any(pixel => pixel != 0);
    var result = inspector.Inspect(image, settings);
    var predictedDefect = !result.IsPass;
    totalInspectionMs += result.ElapsedMilliseconds;

    if (expectedDefect && predictedDefect) truePositive++;
    else if (!expectedDefect && !predictedDefect) trueNegative++;
    else if (!expectedDefect) falsePositive++;
    else falseNegative++;

    if ((index + 1) % 100 == 0) Console.WriteLine($"Processed {index + 1}/{images.Length}");
}

wallClock.Stop();
var metrics = new
{
    Dataset = datasetPath,
    Images = images.Length,
    Settings = settings,
    TruePositive = truePositive,
    TrueNegative = trueNegative,
    FalsePositive = falsePositive,
    FalseNegative = falseNegative,
    Accuracy = Divide(truePositive + trueNegative, images.Length),
    Precision = Divide(truePositive, truePositive + falsePositive),
    Recall = Divide(truePositive, truePositive + falseNegative),
    Specificity = Divide(trueNegative, trueNegative + falsePositive),
    AverageInspectionMilliseconds = totalInspectionMs / Math.Max(1, images.Length),
    TotalWallClockSeconds = wallClock.Elapsed.TotalSeconds
};

var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (outputPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, json);
}

return 0;

static GrayImage LoadGray(string path)
{
    var bitmap = new BitmapImage();
    bitmap.BeginInit();
    bitmap.CacheOption = BitmapCacheOption.OnLoad;
    bitmap.UriSource = new Uri(path);
    bitmap.EndInit();
    var gray = new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
    var pixels = new byte[gray.PixelWidth * gray.PixelHeight];
    gray.CopyPixels(pixels, gray.PixelWidth, 0);
    return new GrayImage(gray.PixelWidth, gray.PixelHeight, pixels);
}

static double Divide(int numerator, int denominator) => denominator == 0 ? 0 : numerator / (double)denominator;
