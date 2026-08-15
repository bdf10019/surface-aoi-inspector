namespace AoiInspector.Ai;

public sealed record AiInspectionSettings
{
    public float PixelProbabilityThreshold { get; init; } = 0.5f;
    public double RejectAreaRatio { get; init; } = 0.0005;
}
