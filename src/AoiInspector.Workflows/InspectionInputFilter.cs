namespace AoiInspector.Workflows;

public static class InspectionInputFilter
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"
        };

    private static readonly string[] AnnotationSuffixes =
        ["_GT", "_MASK", "_LABEL", "-MASK", "-LABEL"];

    public static IReadOnlyList<string> Enumerate(
        string folder,
        bool includeSubfolders,
        bool excludeAnnotationImages)
    {
        var searchOption = includeSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        return Directory.EnumerateFiles(folder, "*", searchOption)
            .Where(IsSupportedImage)
            .Where(path => !excludeAnnotationImages || !IsAnnotationImage(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public static bool IsAnnotationImage(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return AnnotationSuffixes.Any(suffix =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
