namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record RetrievalDiagnostics(
    bool RetrievalDegraded,
    bool SemanticUsed,
    string Mode);
