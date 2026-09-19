namespace ContextDepot.Application.Contexts.Enums;

public enum ContextPersistenceOutcome
{
    Created,
    Replaced,
    ReusedExisting,
    KindConflict,
    InvalidTarget,
    NotFound,
    AlreadyArchived,
    ConcurrencyConflict
}
