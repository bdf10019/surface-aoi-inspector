namespace AoiInspector.Workflows;

public sealed record PhysicalMeasurement(
    double DefectAreaSquareMillimeters,
    double? CentroidXMillimeters,
    double? CentroidYMillimeters);

public static class MaskMeasurement
{
    public static PhysicalMeasurement Measure(
        byte[] mask,
        int maskWidth,
        int maskHeight,
        int sourceWidth,
        int sourceHeight,
        double micrometersPerPixel,
        int sourceOffsetX = 0,
        int sourceOffsetY = 0)
    {
        if (maskWidth <= 0 || maskHeight <= 0 || mask.Length != maskWidth * maskHeight)
            throw new ArgumentException("遮罩尺寸與資料長度不一致。", nameof(mask));
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        if (micrometersPerPixel <= 0 || !double.IsFinite(micrometersPerPixel))
            throw new ArgumentOutOfRangeException(nameof(micrometersPerPixel));

        long count = 0;
        double sumX = 0;
        double sumY = 0;
        for (var y = 0; y < maskHeight; y++)
            for (var x = 0; x < maskWidth; x++)
            {
                if (mask[(y * maskWidth) + x] == 0) continue;
                count++;
                sumX += x + 0.5;
                sumY += y + 0.5;
            }

        var sourcePixelsPerMaskPixel = (sourceWidth / (double)maskWidth) *
                                       (sourceHeight / (double)maskHeight);
        var area = count *
                   sourcePixelsPerMaskPixel *
                   micrometersPerPixel *
                   micrometersPerPixel /
                   1_000_000d;

        if (count == 0)
            return new PhysicalMeasurement(area, null, null);

        var centroidX = (sourceOffsetX + sumX / count * sourceWidth / maskWidth) *
            micrometersPerPixel / 1000d;
        var centroidY = (sourceOffsetY + sumY / count * sourceHeight / maskHeight) *
            micrometersPerPixel / 1000d;
        return new PhysicalMeasurement(area, centroidX, centroidY);
    }
}
