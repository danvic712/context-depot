namespace ContextDepot.Application.Contexts.Enums;

public enum SaveContextOutcome
{
    Created,
    UpdatedCurrentTruth,
    ReusedExisting,
    SupersededExisting
}
