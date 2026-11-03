namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record BudgetReservation(string Status, DateTimeOffset? WaitUntil = null);
