namespace AoiInspector.Workflows;

public sealed record BatchInspectionProgress(
    int CurrentIndex,
    int TotalCount,
    string FilePath);

public sealed record BatchInspectionRunResult(
    IReadOnlyList<InspectionLogEntry> Entries,
    bool IsCancelled)
{
    public int ErrorCount => Entries.Count(entry =>
        entry.ProcessingStatus == InspectionProcessingStatus.Error);

    public int NgCount => Entries.Count(entry =>
        entry.ProcessingStatus == InspectionProcessingStatus.Completed &&
        entry.Verdict == InspectionVerdict.Ng);

    public int ReviewCount => Entries.Count(entry => entry.RequiresReview);

    public bool IsOk =>
        !IsCancelled && ErrorCount == 0 && NgCount == 0 && ReviewCount == 0;
}

public static class BatchInspectionRunner
{
    public static async Task<BatchInspectionRunResult> RunAsync(
        IReadOnlyList<string> files,
        string csvPath,
        Func<string, CancellationToken, Task<InspectionLogEntry>> inspectAsync,
        Func<string, Exception, InspectionLogEntry> createErrorEntry,
        IProgress<BatchInspectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entries = new List<InspectionLogEntry>();
        using var writer = InspectionCsv.CreateWriter(csvPath);

        for (var index = 0; index < files.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new BatchInspectionRunResult(entries, true);

            var path = files[index];
            progress?.Report(new BatchInspectionProgress(index + 1, files.Count, path));

            try
            {
                var entry = await inspectAsync(path, cancellationToken);
                entries.Add(entry);
                writer.Write(entry);

                if (cancellationToken.IsCancellationRequested)
                    return new BatchInspectionRunResult(entries, true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new BatchInspectionRunResult(entries, true);
            }
            catch (Exception ex)
            {
                var entry = createErrorEntry(path, ex.GetBaseException());
                entries.Add(entry);
                writer.Write(entry);
            }
        }

        return new BatchInspectionRunResult(entries, false);
    }
}
