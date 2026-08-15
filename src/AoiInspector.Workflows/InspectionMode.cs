namespace AoiInspector.Workflows;

public enum InspectionMode
{
    Traditional,
    Ai,
    Compare,
    Hybrid
}

public enum HybridDecisionRule
{
    AnyNg,
    AiPrimary,
    Consensus
}

public enum InspectionVerdict
{
    Ok,
    Ng,
    Review
}
