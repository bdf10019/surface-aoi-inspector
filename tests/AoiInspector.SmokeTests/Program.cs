using AoiInspector.Core;

const int width = 128;
const int height = 96;
var inspector = new SurfaceDefectInspector();
var settings = new InspectionSettings
{
    BlurRadius = 8,
    DifferenceThreshold = 28,
    MinimumDefectArea = 10,
    RejectAreaRatio = 0.0005
};

var normalPixels = Enumerable.Repeat((byte)128, width * height).ToArray();
var normalResult = inspector.Inspect(new GrayImage(width, height, normalPixels), settings);
Assert(normalResult.IsPass, "均勻表面應判定為 OK");

var defectPixels = normalPixels.ToArray();
for (var y = 36; y < 48; y++)
for (var x = 50; x < 66; x++)
    defectPixels[(y * width) + x] = 24;

var defectResult = inspector.Inspect(new GrayImage(width, height, defectPixels), settings);
Assert(!defectResult.IsPass, "明顯瑕疵應判定為 NG");
Assert(defectResult.DefectRegionCount > 0, "應找出至少一個瑕疵區域");

Console.WriteLine($"Smoke tests passed. Defect regions: {defectResult.DefectRegionCount}, ratio: {defectResult.DefectAreaRatio:P3}");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
