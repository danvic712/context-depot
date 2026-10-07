namespace ContextDepot.Domain.Setup;

public sealed class InstallationState
{
    public const string InstallationScope = "installation";
    public const string Pending = "pending";
    public const string InProgress = "inProgress";
    public const string Completed = "completed";

    private InstallationState() { }

    public InstallationState(Guid id, DateTimeOffset now)
    {
        Id = id;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Scope { get; private set; } = InstallationScope;
    public string State { get; private set; } = Pending;
    public Guid? InitialDepotId { get; private set; }
    public Guid? InitialWorkspaceId { get; private set; }
    public bool InferenceReviewed { get; private set; }
    public bool AccessKeyReviewed { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Begin(Guid depotId, Guid workspaceId, DateTimeOffset now)
    {
        InitialDepotId = depotId;
        InitialWorkspaceId = workspaceId;
        State = InProgress;
        UpdatedAt = now;
    }

    public void ReviewInference(DateTimeOffset now)
    {
        InferenceReviewed = true;
        UpdatedAt = now;
    }

    public void ReviewAccessKey(DateTimeOffset now)
    {
        AccessKeyReviewed = true;
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        State = Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }
}
