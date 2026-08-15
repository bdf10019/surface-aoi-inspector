namespace AoiInspector.Core;

public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "影像尺寸必須大於零。");
        if (pixels.Length != width * height)
            throw new ArgumentException("像素數量與影像尺寸不相符。", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
}
