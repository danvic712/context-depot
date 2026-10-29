namespace ContextDepot.Application.AgentSessions.Dtos;

/// <summary>Message-level counts; SavedTurns counts successful extraction rounds, not Context items.</summary>
public sealed record SessionEventCounts(int Pending, int Processing, int Completed, int Failed,
    int Unpaired, int SavedTurns);
