namespace AoiInspector.Workflows;

public sealed record InspectionRoi(
    double X = 0,
    double Y = 0,
    double Width = 1,
    double Height = 1)
{
    public static InspectionRoi FullFrame { get; } = new();

    public bool IsFullFrame => X == 0 && Y == 0 && Width == 1 && Height == 1;

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) ||
            !double.IsFinite(Width) || !double.IsFinite(Height))
            throw new InvalidDataException("ROI 必須是有限數值。");
        if (X < 0 || Y < 0 || Width <= 0 || Height <= 0 ||
            X + Width > 1.000000001 || Y + Height > 1.000000001)
            throw new InvalidDataException("ROI 必須位於影像範圍內，且寬高大於零。");
    }

    public PixelRoi ToPixelRoi(int imageWidth, int imageHeight)
    {
        Validate();
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth));

        var left = Math.Clamp((int)Math.Floor(X * imageWidth), 0, imageWidth - 1);
        var top = Math.Clamp((int)Math.Floor(Y * imageHeight), 0, imageHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling((X + Width) * imageWidth), left + 1, imageWidth);
        var bottom = Math.Clamp((int)Math.Ceiling((Y + Height) * imageHeight), top + 1, imageHeight);
        return new PixelRoi(left, top, right - left, bottom - top);
    }
}

public sealed record PixelRoi(int X, int Y, int Width, int Height);
