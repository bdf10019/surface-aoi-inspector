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
    TestInputFiltering(testDirectory);
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
    Assert(loaded.RecipeVersion == source.RecipeVersion, "配方版本往返後不一致");
    Assert(loaded.Mode == InspectionMode.Hybrid, "配方模式往返後不一致");
    Assert(loaded.TraditionalSettings.DifferenceThreshold == 32, "傳統參數往返後不一致");
    Assert(RecipeFile.ComputeSha256(source) == RecipeFile.ComputeSha256(loaded),
        "相同配方的雜湊應一致");
    Assert(RecipeFile.ComputeSha256(source) != RecipeFile.ComputeSha256(source with { ProductCode = "ANOTHER" }),
        "配方內容變更後雜湊應改變");
}

static void TestCsv(string directory)
{
    var path = Path.Combine(directory, "inspection.csv");
    var entry = new InspectionLogEntry(
        DateTimeOffset.Parse("2026-01-02T03:04:05+08:00"),
        "LOT-001",
        "PART-001",
        "part,\"01\".png",
        "nested/part,\"01\".png",
        "測試配方",
        "1.2",
        new string('a', 64),
        InspectionMode.Hybrid,
        InspectionProcessingStatus.Completed,
        InspectionVerdict.Review,
        true,
        "METHODS_DISAGREE",
        string.Empty,
        5,
        "camera-a",
        "light-a",
        0.01,
        0.02,
        0.1,
        0.2,
        12.5);

    using (var writer = InspectionCsv.CreateWriter(path))
    {
        writer.Write(entry);
        writer.Write(entry with
        {
            FileName = "broken.png",
            SourceRelativePath = "nested/broken.png",
            ProcessingStatus = InspectionProcessingStatus.Error,
            Verdict = InspectionVerdict.Review,
            ReasonCode = "IMAGE_PROCESSING_ERROR",
            ErrorMessage = "decode failed, invalid header",
            TraditionalAreaRatio = null,
            AiAreaRatio = null
        });
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert(reader.ReadToEnd().Contains("METHODS_DISAGREE"),
            "CSV 應在每筆完成後立即寫入磁碟");
    }
    var csv = File.ReadAllText(path, Encoding.UTF8);
    Assert(csv.Contains("\"part,\"\"01\"\".png\""), "CSV 特殊字元未正確跳脫");
    Assert(csv.Contains("METHODS_DISAGREE"), "CSV 缺少判定原因");
    Assert(csv.Contains("recipe_sha256"), "CSV 缺少配方雜湊欄位");
    Assert(csv.Contains("micrometers_per_pixel"), "CSV 缺少校正值欄位");
    Assert(csv.Contains("IMAGE_PROCESSING_ERROR"), "CSV 缺少單檔錯誤紀錄");
    Assert(csv.Contains("\"decode failed, invalid header\""), "CSV 錯誤訊息未正確跳脫");
}

static void TestInputFiltering(string directory)
{
    var inputDirectory = Path.Combine(directory, "inputs");
    Directory.CreateDirectory(inputDirectory);
    File.WriteAllText(Path.Combine(inputDirectory, "part-01.png"), string.Empty);
    File.WriteAllText(Path.Combine(inputDirectory, "part-01_GT.png"), string.Empty);
    File.WriteAllText(Path.Combine(inputDirectory, "notes.txt"), string.Empty);
    var nestedDirectory = Path.Combine(inputDirectory, "nested");
    Directory.CreateDirectory(nestedDirectory);
    File.WriteAllText(Path.Combine(nestedDirectory, "part-02.jpg"), string.Empty);

    var filtered = InspectionInputFilter.Enumerate(inputDirectory, false, true);
    Assert(filtered.Count == 1 && Path.GetFileName(filtered[0]) == "part-01.png",
        "批次輸入應排除標註影像與非影像檔");
    Assert(InspectionInputFilter.Enumerate(inputDirectory, true, true).Count == 2,
        "啟用子資料夾後應包含巢狀影像");
    Assert(InspectionInputFilter.IsAnnotationImage("sample-mask.TIFF"),
        "標註遮罩檔名辨識失敗");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
