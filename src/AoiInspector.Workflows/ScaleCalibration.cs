namespace AoiInspector.Workflows;

public static class ScaleCalibration
{
    public static double CalculateMicrometersPerPixel(
        double knownLengthMillimeters,
        double measuredPixels)
    {
        if (knownLengthMillimeters <= 0 || !double.IsFinite(knownLengthMillimeters))
            throw new ArgumentOutOfRangeException(nameof(knownLengthMillimeters));
        if (measuredPixels <= 0 || !double.IsFinite(measuredPixels))
            throw new ArgumentOutOfRangeException(nameof(measuredPixels));

        return knownLengthMillimeters * 1000d / measuredPixels;
    }
}
