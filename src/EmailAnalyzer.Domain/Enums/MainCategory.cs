namespace EmailAnalyzer.Domain.Enums;

/// <summary>
/// Top-level classification categories produced by the AI. Persisted as string
/// (see EmailMessageConfiguration). Definitions are embedded verbatim into the
/// AI system prompt (see spec section 5).
/// </summary>
public enum MainCategory
{
    ERP,
    MuhasebeFinans,
    EDonusum,
    CRM,
    IK,
    RaporlamaAnalitik,
    EgitimDanismanlik,
    Diger
}
