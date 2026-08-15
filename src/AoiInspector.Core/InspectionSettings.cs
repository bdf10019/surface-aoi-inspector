namespace AoiInspector.Core;

public sealed record InspectionSettings
{
    public int BlurRadius { get; init; } = 7;
    public int DifferenceThreshold { get; init; } = 32;
    public int MinimumDefectArea { get; init; } = 12;
    public double RejectAreaRatio { get; init; } = 0.001;
}
