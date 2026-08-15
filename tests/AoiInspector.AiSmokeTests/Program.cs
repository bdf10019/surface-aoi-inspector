using AoiInspector.Ai;
using AoiInspector.Core;

var modelPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath("models/local/ksdd2-v1/surface_defect_segmenter.onnx");

const int width = 640;
const int height = 256;
var pixels = new byte[width * height * 3];
Array.Fill(pixels, (byte)154);
for (var x = 170; x < 470; x++)
for (var y = 120; y < 124; y++)
{
    var index = ((y * width) + x) * 3;
    pixels[index] = pixels[index + 1] = pixels[index + 2] = 35;
}

using var inspector = new OnnxSurfaceDefectInspector(modelPath);
var result = inspector.Inspect(new RgbImage(width, height, pixels), new AiInspectionSettings());

Assert(result.MaskWidth == width && result.MaskHeight == height, "AI 遮罩尺寸錯誤");
Assert(result.DefectMask.Length == width * height, "AI 遮罩像素數錯誤");
Assert(result.DefectAreaRatio is >= 0 and <= 1, "AI 面積比例超出範圍");
Assert(result.InferenceMilliseconds > 0, "AI 推論時間必須大於零");

Console.WriteLine(
    $"AI smoke test passed. Verdict: {(result.IsPass ? "OK" : "NG")}, " +
    $"ratio: {result.DefectAreaRatio:P3}, inference: {result.InferenceMilliseconds:F2} ms");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
