using System.Globalization;
using System.Text;

namespace AoiInspector.Workflows;

public enum InspectionProcessingStatus
{
    Completed,
    Error
}

public sealed record InspectionLogEntry(
    DateTimeOffset Timestamp,
    string BatchId,
    string ProductCode,
    string FileName,
    string SourceRelativePath,
    string SourceSha256,
    string RecipeName,
    string RecipeVersion,
    string RecipeSha256,
    string ApplicationVersion,
    string TraditionalAlgorithmVersion,
    string AiAlgorithmVersion,
    string ModelGraphSha256,
    string ModelDataSha256,
    InspectionMode Mode,
    InspectionProcessingStatus ProcessingStatus,
    InspectionVerdict Verdict,
    bool RequiresReview,
    string ReasonCode,
    string ErrorMessage,
    double MicrometersPerPixel,
    string CameraProfile,
    string LightingProfile,
    double? TraditionalAreaRatio,
    double? AiAreaRatio,
    double? TraditionalAreaSquareMillimeters,
    double? AiAreaSquareMillimeters,
    double ElapsedMilliseconds);

public static class InspectionCsv
{
    public static InspectionCsvWriter CreateWriter(string path) => new(path);

    public static void Write(string path, IEnumerable<InspectionLogEntry> entries)
    {
        using var writer = CreateWriter(path);
        foreach (var entry in entries)
            writer.Write(entry);
    }
}

public sealed class InspectionCsvWriter : IDisposable
{
    private static readonly string[] Header =
    [
        "timestamp", "batch_id", "product_code", "file_name", "source_relative_path", "source_sha256",
        "recipe", "recipe_version", "recipe_sha256", "application_version",
        "traditional_algorithm_version", "ai_algorithm_version", "model_graph_sha256",
        "model_data_sha256", "mode", "processing_status",
        "verdict", "requires_review", "reason_code", "error_message",
        "micrometers_per_pixel", "camera_profile", "lighting_profile",
        "traditional_area_ratio", "ai_area_ratio", "traditional_area_mm2",
        "ai_area_mm2", "elapsed_ms"
    ];

    private readonly StreamWriter _writer;

    internal InspectionCsvWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(true));
        _writer.WriteLine(string.Join(',', Header));
        _writer.Flush();
    }

    public void Write(InspectionLogEntry entry)
    {
        var values = new[]
        {
            entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            entry.BatchId,
            entry.ProductCode,
            entry.FileName,
            entry.SourceRelativePath,
            entry.SourceSha256,
            entry.RecipeName,
            entry.RecipeVersion,
            entry.RecipeSha256,
            entry.ApplicationVersion,
            entry.TraditionalAlgorithmVersion,
            entry.AiAlgorithmVersion,
            entry.ModelGraphSha256,
            entry.ModelDataSha256,
            entry.Mode.ToString(),
            entry.ProcessingStatus.ToString(),
            entry.Verdict.ToString(),
            entry.RequiresReview ? "true" : "false",
            entry.ReasonCode,
            entry.ErrorMessage,
            entry.MicrometersPerPixel.ToString("0.########", CultureInfo.InvariantCulture),
            entry.CameraProfile,
            entry.LightingProfile,
            Format(entry.TraditionalAreaRatio),
            Format(entry.AiAreaRatio),
            Format(entry.TraditionalAreaSquareMillimeters),
            Format(entry.AiAreaSquareMillimeters),
            entry.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
        };
        _writer.WriteLine(string.Join(',', values.Select(Escape)));
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();

    private static string Format(double? value) =>
        value?.ToString("0.########", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
