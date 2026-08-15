using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AoiInspector.Ai;
using AoiInspector.Core;

var datasetPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath("data/raw/KolektorSDD2/test");
var modelPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath("models/local/ksdd2-v1/surface_defect_segmenter.onnx");
var outputPath = args.Length > 2
    ? Path.GetFullPath(args[2])
    : Path.GetFullPath("outputs/ai-csharp-test-metrics.json");

if (!Directory.Exists(datasetPath))
    throw new DirectoryNotFoundException($"找不到測試資料夾：{datasetPath}");

var samples = DiscoverSamples(datasetPath);
using var inspector = new OnnxSurfaceDefectInspector(modelPath);
var settings = new AiInspectionSettings();
var totalTimer = Stopwatch.StartNew();
var inferenceTimes = new List<double>(samples.Count);
var truePositive = 0;
var trueNegative = 0;
var falsePositive = 0;
var falseNegative = 0;

for (var index = 0; index < samples.Count; index++)
{
    var sample = samples[index];
    var expected = MaskHasDefect(sample.MaskPath);
    var result = inspector.Inspect(LoadRgbImage(sample.ImagePath), settings);
    var predicted = !result.IsPass;
    inferenceTimes.Add(result.InferenceMilliseconds);

    if (predicted && expected) truePositive++;
    else if (!predicted && !expected) trueNegative++;
    else if (predicted) falsePositive++;
    else falseNegative++;

    if ((index + 1) % 100 == 0 || index + 1 == samples.Count)
        Console.WriteLine($"Evaluated {index + 1}/{samples.Count}");
}

totalTimer.Stop();
inferenceTimes.Sort();
var precision = Divide(truePositive, truePositive + falsePositive);
var recall = Divide(truePositive, truePositive + falseNegative);
var specificity = Divide(trueNegative, trueNegative + falsePositive);
var report = new
{
    split = "official_test",
    implementation = "C# / ONNX Runtime CPU",
    images = samples.Count,
    confusion_matrix = new
    {
        true_positive = truePositive,
        true_negative = trueNegative,
        false_positive = falsePositive,
        false_negative = falseNegative
    },
    metrics = new
    {
        accuracy = Divide(truePositive + trueNegative, samples.Count),
        precision,
        recall,
        specificity,
        balanced_accuracy = (recall + specificity) / 2.0
    },
    timing = new
    {
        total_seconds = totalTimer.Elapsed.TotalSeconds,
        mean_inference_ms = inferenceTimes.Average(),
        median_inference_ms = Percentile(inferenceTimes, 0.50),
        p95_inference_ms = Percentile(inferenceTimes, 0.95)
    },
    decision_rule = new
    {
        pixel_probability_threshold = settings.PixelProbabilityThreshold,
        minimum_defect_area_ratio = settings.RejectAreaRatio
    }
};

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outputPath, json);
Console.WriteLine(json);
Console.WriteLine($"CSHARP_METRICS_READY {outputPath}");

static List<(string ImagePath, string MaskPath)> DiscoverSamples(string folder)
{
    var samples = new List<(string, string)>();
    foreach (var imagePath in Directory.EnumerateFiles(folder, "*.png").Order())
    {
        var stem = Path.GetFileNameWithoutExtension(imagePath);
        if (stem.Contains("_GT", StringComparison.Ordinal)) continue;

        var maskPath = Path.Combine(folder, $"{stem}_GT.png");
        if (!File.Exists(maskPath) && stem.EndsWith(" (copy)", StringComparison.Ordinal))
        {
            var baseStem = stem[..^" (copy)".Length];
            maskPath = Path.Combine(folder, $"{baseStem}_GT (copy).png");
        }

        if (!File.Exists(maskPath))
            throw new FileNotFoundException("找不到對應標註遮罩。", maskPath);
        samples.Add((imagePath, maskPath));
    }

    if (samples.Count == 0) throw new InvalidOperationException("測試資料夾沒有影像。");
    return samples;
}

static RgbImage LoadRgbImage(string path)
{
    using var stream = File.OpenRead(path);
    var decoder = BitmapDecoder.Create(
        stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
    var stride = converted.PixelWidth * 4;
    var bgra = new byte[stride * converted.PixelHeight];
    converted.CopyPixels(bgra, stride, 0);
    var rgb = new byte[converted.PixelWidth * converted.PixelHeight * 3];
    for (var index = 0; index < converted.PixelWidth * converted.PixelHeight; index++)
    {
        rgb[index * 3] = bgra[index * 4 + 2];
        rgb[index * 3 + 1] = bgra[index * 4 + 1];
        rgb[index * 3 + 2] = bgra[index * 4];
    }

    return new RgbImage(converted.PixelWidth, converted.PixelHeight, rgb);
}

static bool MaskHasDefect(string path)
{
    using var stream = File.OpenRead(path);
    var decoder = BitmapDecoder.Create(
        stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Gray8, null, 0);
    var pixels = new byte[converted.PixelWidth * converted.PixelHeight];
    converted.CopyPixels(pixels, converted.PixelWidth, 0);
    return pixels.Any(value => value != 0);
}

static double Divide(int numerator, int denominator) =>
    denominator == 0 ? 0 : numerator / (double)denominator;

static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
{
    var index = (sortedValues.Count - 1) * percentile;
    var lower = (int)Math.Floor(index);
    var upper = (int)Math.Ceiling(index);
    if (lower == upper) return sortedValues[lower];
    var weight = index - lower;
    return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * weight);
}
