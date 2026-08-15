using System.Diagnostics;

namespace AoiInspector.Core;

public sealed class SurfaceDefectInspector
{
    public InspectionResult Inspect(GrayImage image, InspectionSettings settings)
    {
        Validate(settings);
        var timer = Stopwatch.StartNew();
        var candidateMask = FindLocalDifferences(image, settings);
        var (cleanMask, regionCount, defectPixels) = RemoveSmallRegions(
            candidateMask, image.Width, image.Height, settings.MinimumDefectArea);

        timer.Stop();
        var areaRatio = defectPixels / (double)(image.Width * image.Height);
        return new InspectionResult(
            areaRatio < settings.RejectAreaRatio,
            regionCount,
            defectPixels,
            areaRatio,
            timer.Elapsed.TotalMilliseconds,
            cleanMask);
    }

    private static byte[] FindLocalDifferences(GrayImage image, InspectionSettings settings)
    {
        var width = image.Width;
        var height = image.Height;
        var integralWidth = width + 1;
        var integral = new long[integralWidth * (height + 1)];

        for (var y = 0; y < height; y++)
        {
            long rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                rowSum += image.Pixels[(y * width) + x];
                integral[((y + 1) * integralWidth) + x + 1] =
                    integral[(y * integralWidth) + x + 1] + rowSum;
            }
        }

        var mask = new byte[width * height];
        var radius = settings.BlurRadius;
        for (var y = 0; y < height; y++)
        {
            var top = Math.Max(0, y - radius);
            var bottom = Math.Min(height - 1, y + radius);
            for (var x = 0; x < width; x++)
            {
                var left = Math.Max(0, x - radius);
                var right = Math.Min(width - 1, x + radius);
                var sum = integral[((bottom + 1) * integralWidth) + right + 1]
                          - integral[(top * integralWidth) + right + 1]
                          - integral[((bottom + 1) * integralWidth) + left]
                          + integral[(top * integralWidth) + left];
                var area = (right - left + 1) * (bottom - top + 1);
                var localMean = sum / (double)area;
                if (Math.Abs(image.Pixels[(y * width) + x] - localMean) >= settings.DifferenceThreshold)
                    mask[(y * width) + x] = 255;
            }
        }

        return mask;
    }

    private static (byte[] Mask, int RegionCount, int PixelCount) RemoveSmallRegions(
        byte[] source, int width, int height, int minimumArea)
    {
        var result = new byte[source.Length];
        var visited = new bool[source.Length];
        var queue = new int[source.Length];
        var component = new List<int>();
        var regionCount = 0;
        var pixelCount = 0;

        for (var start = 0; start < source.Length; start++)
        {
            if (source[start] == 0 || visited[start]) continue;
            component.Clear();
            var head = 0;
            var tail = 0;
            queue[tail++] = start;
            visited[start] = true;

            while (head < tail)
            {
                var current = queue[head++];
                component.Add(current);
                var x = current % width;
                var y = current / width;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0) continue;
                        var nextX = x + offsetX;
                        var nextY = y + offsetY;
                        if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) continue;
                        var next = (nextY * width) + nextX;
                        if (!visited[next] && source[next] != 0)
                        {
                            visited[next] = true;
                            queue[tail++] = next;
                        }
                    }
                }
            }

            if (component.Count < minimumArea) continue;
            regionCount++;
            pixelCount += component.Count;
            foreach (var index in component) result[index] = 255;
        }

        return (result, regionCount, pixelCount);
    }

    private static void Validate(InspectionSettings settings)
    {
        if (settings.BlurRadius < 1 || settings.DifferenceThreshold is < 1 or > 255 ||
            settings.MinimumDefectArea < 1 || settings.RejectAreaRatio is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "檢測參數超出有效範圍。");
    }
}
