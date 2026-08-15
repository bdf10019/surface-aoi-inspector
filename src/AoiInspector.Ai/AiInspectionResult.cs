namespace AoiInspector.Ai;

public sealed record AiInspectionResult(
    bool IsPass,
    int DefectRegionCount,
    int DefectPixelCount,
    double DefectAreaRatio,
    double PreprocessMilliseconds,
    double InferenceMilliseconds,
    double PostprocessMilliseconds,
    int MaskWidth,
    int MaskHeight,
    byte[] DefectMask)
{
    public double ElapsedMilliseconds =>
        PreprocessMilliseconds + InferenceMilliseconds + PostprocessMilliseconds;
}
