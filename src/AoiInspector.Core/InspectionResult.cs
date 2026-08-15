namespace AoiInspector.Core;

public sealed record InspectionResult(
    bool IsPass,
    int DefectRegionCount,
    int DefectPixelCount,
    double DefectAreaRatio,
    double ElapsedMilliseconds,
    byte[] DefectMask);
