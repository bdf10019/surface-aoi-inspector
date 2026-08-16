namespace AoiInspector.Core;

public sealed class RgbImage
{
    public RgbImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "影像尺寸必須大於零。");
        if (pixels.Length != width * height * 3)
            throw new ArgumentException("RGB 像素數量與影像尺寸不相符。", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public GrayImage ToGrayImage()
    {
        var gray = new byte[Width * Height];
        for (var index = 0; index < gray.Length; index++)
        {
            var source = index * 3;
            gray[index] = (byte)Math.Clamp(
                Math.Round((Pixels[source] * 0.299) + (Pixels[source + 1] * 0.587) +
                           (Pixels[source + 2] * 0.114)), 0, 255);
        }

        return new GrayImage(Width, Height, gray);
    }

    public RgbImage Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
            x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(x), "裁切範圍超出影像邊界。");

        var cropped = new byte[width * height * 3];
        for (var targetY = 0; targetY < height; targetY++)
        {
            var sourceOffset = (((y + targetY) * Width) + x) * 3;
            var targetOffset = targetY * width * 3;
            Array.Copy(Pixels, sourceOffset, cropped, targetOffset, width * 3);
        }
        return new RgbImage(width, height, cropped);
    }
}
