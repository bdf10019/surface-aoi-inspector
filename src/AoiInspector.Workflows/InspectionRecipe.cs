using AoiInspector.Ai;
using AoiInspector.Core;

namespace AoiInspector.Workflows;

public sealed record InspectionRecipe
{
    public int SchemaVersion { get; init; } = 1;
    public string Name { get; init; } = "一般表面檢測";
    public string Description { get; init; } = string.Empty;
    public InspectionMode Mode { get; init; } = InspectionMode.Hybrid;
    public HybridDecisionRule HybridRule { get; init; } = HybridDecisionRule.AnyNg;
    public InspectionSettings TraditionalSettings { get; init; } = new();
    public AiInspectionSettings AiSettings { get; init; } = new();
    public double MicrometersPerPixel { get; init; } = 5.0;
    public string CameraProfile { get; init; } = "待現場設定";
    public string LightingProfile { get; init; } = "待現場設定";

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException($"不支援的配方版本：{SchemaVersion}");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("配方名稱不可空白。");
        if (!Enum.IsDefined(Mode))
            throw new InvalidDataException("檢測模式無效。");
        if (!Enum.IsDefined(HybridRule))
            throw new InvalidDataException("融合規則無效。");
        if (MicrometersPerPixel <= 0 || !double.IsFinite(MicrometersPerPixel))
            throw new InvalidDataException("像素尺寸必須是大於零的有限數值。");
        if (TraditionalSettings.BlurRadius < 0)
            throw new InvalidDataException("模糊半徑不可小於零。");
        if (TraditionalSettings.DifferenceThreshold is < 0 or > 255)
            throw new InvalidDataException("差異閾值必須介於 0 到 255。");
        if (TraditionalSettings.MinimumDefectArea < 1)
            throw new InvalidDataException("最小瑕疵面積必須大於零。");
        if (TraditionalSettings.RejectAreaRatio is < 0 or > 1)
            throw new InvalidDataException("傳統法拒收面積比必須介於 0 到 1。");
        if (AiSettings.PixelProbabilityThreshold is < 0 or > 1)
            throw new InvalidDataException("AI 像素閾值必須介於 0 到 1。");
        if (AiSettings.RejectAreaRatio is < 0 or > 1)
            throw new InvalidDataException("AI 拒收面積比必須介於 0 到 1。");
    }
}

public static class RecipeCatalog
{
    public static IReadOnlyList<InspectionRecipe> BuiltIn { get; } =
    [
        new InspectionRecipe
        {
            Name = "精密金屬表面－起始配方",
            Description = "供離線驗證使用的起始值；上線前須依鏡頭、治具、光源與量測標準重新校正。",
            Mode = InspectionMode.Hybrid,
            HybridRule = HybridDecisionRule.AnyNg,
            TraditionalSettings = new InspectionSettings
            {
                BlurRadius = 7,
                DifferenceThreshold = 32,
                MinimumDefectArea = 12,
                RejectAreaRatio = 0.001
            },
            AiSettings = new AiInspectionSettings
            {
                PixelProbabilityThreshold = 0.5f,
                RejectAreaRatio = 0.0005
            },
            MicrometersPerPixel = 5.0,
            CameraProfile = "同軸相機－待現場校正",
            LightingProfile = "低角度環形光－待現場驗證"
        }
    ];
}
