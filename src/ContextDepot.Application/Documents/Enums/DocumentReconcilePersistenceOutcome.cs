namespace ContextDepot.Application.Documents.Enums;

public enum DocumentReconcilePersistenceOutcome
{
    Created,
    Reconciled,
    Reactivated,
    ConcurrencyConflict
}
