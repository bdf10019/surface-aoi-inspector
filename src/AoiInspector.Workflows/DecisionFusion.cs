namespace AoiInspector.Workflows;

public sealed record FusionDecision(
    InspectionVerdict Verdict,
    bool RequiresReview,
    string ReasonCode);

public static class DecisionFusion
{
    public static FusionDecision Evaluate(
        bool traditionalPass,
        bool aiPass,
        HybridDecisionRule rule)
    {
        if (traditionalPass && aiPass)
            return new FusionDecision(InspectionVerdict.Ok, false, "BOTH_OK");

        if (!traditionalPass && !aiPass)
            return new FusionDecision(InspectionVerdict.Ng, false, "BOTH_NG");

        return rule switch
        {
            HybridDecisionRule.AnyNg => new FusionDecision(
                InspectionVerdict.Ng,
                true,
                "SINGLE_METHOD_NG"),
            HybridDecisionRule.AiPrimary => new FusionDecision(
                InspectionVerdict.Review,
                true,
                aiPass ? "AI_OK_TRADITIONAL_NG" : "AI_NG_TRADITIONAL_OK"),
            HybridDecisionRule.Consensus => new FusionDecision(
                InspectionVerdict.Review,
                true,
                "METHODS_DISAGREE"),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null)
        };
    }
}
