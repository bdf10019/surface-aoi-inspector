using System.Text;
using AoiInspector.Workflows;

var testDirectory = Path.Combine(Path.GetTempPath(), $"aoi-workflow-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(testDirectory);

try
{
    TestFusionRules();
    TestPhysicalMeasurement();
    TestRecipeRoundTrip(testDirectory);
    TestCsv(testDirectory);
    if (args.Length > 0)
    {
        var sampleRecipe = RecipeFile.Load(args[0]);
        Assert(sampleRecipe.SchemaVersion == 1, "範例配方版本無效");
    }
    Console.WriteLine("Workflow tests passed.");
}
finally
{
    Directory.Delete(testDirectory, true);
}

static void TestFusionRules()
{
    Assert(DecisionFusion.Evaluate(true, true, HybridDecisionRule.AnyNg).Verdict == InspectionVerdict.Ok,
        "雙 OK 應為 OK");
    var anyNg = DecisionFusion.Evaluate(true, false, HybridDecisionRule.AnyNg);
    Assert(anyNg.Verdict == InspectionVerdict.Ng && anyNg.RequiresReview,
        "任一 NG 規則的分歧結果應為 NG 並要求複核");
    Assert(DecisionFusion.Evaluate(true, false, HybridDecisionRule.Consensus).Verdict == InspectionVerdict.Review,
        "一致規則的分歧結果應轉人工複核");
}

static void TestPhysicalMeasurement()
{
    var mask = new byte[4];
    mask[0] = 255;
    var measurement = MaskMeasurement.Measure(mask, 2, 2, 4, 4, 10);
    Assert(Math.Abs(measurement.DefectAreaSquareMillimeters - 0.0004) < 1e-12,
        "遮罩面積換算錯誤");
    Assert(measurement.CentroidXMillimeters is > 0, "有瑕疵時應回傳中心位置");
}

static void TestRecipeRoundTrip(string directory)
{
    var path = Path.Combine(directory, "recipe.json");
    var source = RecipeCatalog.BuiltIn[0] with { Name = "測試配方" };
    RecipeFile.Save(path, source);
    var loaded = RecipeFile.Load(path);
    Assert(loaded.Name == source.Name, "配方名稱往返後不一致");
    Assert(loaded.Mode == InspectionMode.Hybrid, "配方模式往返後不一致");
    Assert(loaded.TraditionalSettings.DifferenceThreshold == 32, "傳統參數往返後不一致");
}

static void TestCsv(string directory)
{
    var path = Path.Combine(directory, "inspection.csv");
    InspectionCsv.Write(path,
    [
        new InspectionLogEntry(
            DateTimeOffset.Parse("2026-01-02T03:04:05+08:00"),
            "part,\"01\".png",
            "測試配方",
            InspectionMode.Hybrid,
            InspectionVerdict.Review,
            true,
            "METHODS_DISAGREE",
            0.01,
            0.02,
            0.1,
            0.2,
            12.5)
    ]);
    var csv = File.ReadAllText(path, Encoding.UTF8);
    Assert(csv.Contains("\"part,\"\"01\"\".png\""), "CSV 特殊字元未正確跳脫");
    Assert(csv.Contains("METHODS_DISAGREE"), "CSV 缺少判定原因");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
