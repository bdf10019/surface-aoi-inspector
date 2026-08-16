using System.Diagnostics;
using AoiInspector.Core;
using Microsoft.ML.OnnxRuntime;

namespace AoiInspector.Ai;

public sealed class OnnxSurfaceDefectInspector : IDisposable
{
    public const string PipelineVersion = "lr-aspp-mobilenetv3-ksdd2-v1";
    public const int ModelWidth = 640;
    public const int ModelHeight = 256;
    public const string DefaultModelFileName = "surface_defect_segmenter.onnx";

    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] StandardDeviation = [0.229f, 0.224f, 0.225f];
    private readonly InferenceSession _session;
    private readonly RunOptions _runOptions = new();

    public OnnxSurfaceDefectInspector(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("找不到 AI 模型，請先執行 scripts/Prepare-AiModel.ps1。", modelPath);

        using var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING
        };
        _session = new InferenceSession(modelPath, options);

        if (!_session.InputMetadata.TryGetValue("image", out var input) || input is null ||
            input.Dimensions.Length != 4 || input.Dimensions[1] != 3 ||
            input.Dimensions[2] != ModelHeight || input.Dimensions[3] != ModelWidth)
            throw new InvalidDataException("AI 模型輸入必須是 image: [1, 3, 256, 640]。");
    }

    public AiInspectionResult Inspect(RgbImage image, AiInspectionSettings settings)
    {
        Validate(settings);
        var timer = Stopwatch.StartNew();
        var input = ResizeAndNormalize(image);
        timer.Stop();
        var preprocessMilliseconds = timer.Elapsed.TotalMilliseconds;

        timer.Restart();
        using var inputValue = OrtValue.CreateTensorValueFromMemory(
            input, [1L, 3L, ModelHeight, ModelWidth]);
        var inputs = new Dictionary<string, OrtValue> { ["image"] = inputValue };
        using var outputs = _session.Run(_runOptions, inputs, _session.OutputNames);
        var logits = outputs[0].GetTensorDataAsSpan<float>();
        timer.Stop();
        var inferenceMilliseconds = timer.Elapsed.TotalMilliseconds;

        timer.Restart();
        var mask = new byte[ModelWidth * ModelHeight];
        var defectPixels = 0;
        for (var index = 0; index < mask.Length; index++)
        {
            var probability = Sigmoid(logits[index]);
            if (probability < settings.PixelProbabilityThreshold) continue;
            mask[index] = 255;
            defectPixels++;
        }

        var regions = CountRegions(mask, ModelWidth, ModelHeight);
        var areaRatio = defectPixels / (double)mask.Length;
        timer.Stop();

        return new AiInspectionResult(
            areaRatio < settings.RejectAreaRatio,
            regions,
            defectPixels,
            areaRatio,
            preprocessMilliseconds,
            inferenceMilliseconds,
            timer.Elapsed.TotalMilliseconds,
            ModelWidth,
            ModelHeight,
            mask);
    }

    private static float[] ResizeAndNormalize(RgbImage image)
    {
        var output = new float[3 * ModelWidth * ModelHeight];
        var planeSize = ModelWidth * ModelHeight;
        var scaleX = image.Width / (double)ModelWidth;
        var scaleY = image.Height / (double)ModelHeight;

        for (var targetY = 0; targetY < ModelHeight; targetY++)
        {
            var sourceY = Math.Clamp(((targetY + 0.5) * scaleY) - 0.5, 0, image.Height - 1);
            var y0 = (int)Math.Floor(sourceY);
            var y1 = Math.Min(y0 + 1, image.Height - 1);
            var yWeight = sourceY - y0;

            for (var targetX = 0; targetX < ModelWidth; targetX++)
            {
                var sourceX = Math.Clamp(((targetX + 0.5) * scaleX) - 0.5, 0, image.Width - 1);
                var x0 = (int)Math.Floor(sourceX);
                var x1 = Math.Min(x0 + 1, image.Width - 1);
                var xWeight = sourceX - x0;
                var targetIndex = (targetY * ModelWidth) + targetX;

                for (var channel = 0; channel < 3; channel++)
                {
                    var top = Lerp(
                        image.Pixels[((y0 * image.Width + x0) * 3) + channel],
                        image.Pixels[((y0 * image.Width + x1) * 3) + channel], xWeight);
                    var bottom = Lerp(
                        image.Pixels[((y1 * image.Width + x0) * 3) + channel],
                        image.Pixels[((y1 * image.Width + x1) * 3) + channel], xWeight);
                    var value = Lerp(top, bottom, yWeight) / 255.0;
                    output[(channel * planeSize) + targetIndex] =
                        (float)((value - Mean[channel]) / StandardDeviation[channel]);
                }
            }
        }

        return output;
    }

    private static double Lerp(double left, double right, double weight) =>
        left + ((right - left) * weight);

    private static float Sigmoid(float value) =>
        value >= 0
            ? 1f / (1f + MathF.Exp(-value))
            : MathF.Exp(value) / (1f + MathF.Exp(value));

    private static int CountRegions(byte[] mask, int width, int height)
    {
        var visited = new bool[mask.Length];
        var queue = new int[mask.Length];
        var regionCount = 0;

        for (var start = 0; start < mask.Length; start++)
        {
            if (mask[start] == 0 || visited[start]) continue;
            regionCount++;
            var head = 0;
            var tail = 0;
            queue[tail++] = start;
            visited[start] = true;

            while (head < tail)
            {
                var current = queue[head++];
                var x = current % width;
                var y = current / width;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0) continue;
                        var nextX = x + offsetX;
                        var nextY = y + offsetY;
                        if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) continue;
                        var next = (nextY * width) + nextX;
                        if (mask[next] == 0 || visited[next]) continue;
                        visited[next] = true;
                        queue[tail++] = next;
                    }
            }
        }

        return regionCount;
    }

    private static void Validate(AiInspectionSettings settings)
    {
        if (settings.PixelProbabilityThreshold is <= 0 or >= 1 ||
            settings.RejectAreaRatio is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "AI 檢測參數超出有效範圍。");
    }

    public void Dispose()
    {
        _runOptions.Dispose();
        _session.Dispose();
    }
}
