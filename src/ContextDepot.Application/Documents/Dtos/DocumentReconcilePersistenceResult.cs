using ContextDepot.Application.Documents.Enums;
using ContextDepot.Domain.Documents;

namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentReconcilePersistenceResult(
    Document Document,
    DocumentReconcilePersistenceOutcome Outcome);
