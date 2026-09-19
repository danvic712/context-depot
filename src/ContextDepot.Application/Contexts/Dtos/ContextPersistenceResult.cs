using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextPersistenceResult(
    ContextItem? Context,
    ContextPersistenceOutcome Outcome,
    Guid? PreviousContextId = null);
