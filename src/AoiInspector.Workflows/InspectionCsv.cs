using System.Globalization;
using System.Text;

namespace AoiInspector.Workflows;

public sealed record InspectionLogEntry(
    DateTimeOffset Timestamp,
    string FileName,
    string RecipeName,
    InspectionMode Mode,
    InspectionVerdict Verdict,
    bool RequiresReview,
    string ReasonCode,
    double? TraditionalAreaRatio,
    double? AiAreaRatio,
    double? TraditionalAreaSquareMillimeters,
    double? AiAreaSquareMillimeters,
    double ElapsedMilliseconds);

public static class InspectionCsv
{
    private static readonly string[] Header =
    [
        "timestamp", "file_name", "recipe", "mode", "verdict", "requires_review",
        "reason_code", "traditional_area_ratio", "ai_area_ratio",
        "traditional_area_mm2", "ai_area_mm2", "elapsed_ms"
    ];

    public static void Write(string path, IEnumerable<InspectionLogEntry> entries)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(',', Header));
        foreach (var entry in entries)
        {
            var values = new[]
            {
                entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                entry.FileName,
                entry.RecipeName,
                entry.Mode.ToString(),
                entry.Verdict.ToString(),
                entry.RequiresReview ? "true" : "false",
                entry.ReasonCode,
                Format(entry.TraditionalAreaRatio),
                Format(entry.AiAreaRatio),
                Format(entry.TraditionalAreaSquareMillimeters),
                Format(entry.AiAreaSquareMillimeters),
                entry.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
            };
            writer.WriteLine(string.Join(',', values.Select(Escape)));
        }
    }

    private static string Format(double? value) =>
        value?.ToString("0.########", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
