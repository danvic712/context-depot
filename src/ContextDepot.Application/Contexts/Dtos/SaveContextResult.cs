using ContextDepot.Application.Contexts.Enums;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record SaveContextResult(ContextModel Context, SaveContextOutcome Outcome, Guid? PreviousContextId = null);
