namespace ContextDepot.Domain.Workspaces;

public sealed class WorkspaceSubjectBinding
{
    private WorkspaceSubjectBinding() { }

    public WorkspaceSubjectBinding(Guid id, Guid depotId, Guid workspaceId,
        WorkspaceSubjectType subjectType, string subjectKey, DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        WorkspaceId = workspaceId;
        SubjectType = subjectType;
        SubjectKey = subjectKey;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid DepotId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkspaceSubjectType SubjectType { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
