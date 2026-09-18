namespace ContextDepot.Domain.Entities;

public sealed class Owner
{
    private Owner()
    {
    }

    public Owner(Guid id, string displayName, DateTimeOffset now)
    {
        Id = id;
        DisplayName = displayName;
        CreatedAt = now;
        UpdatedAt = now;
        MetadataJson = "{}";
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<Workspace> Workspaces { get; } = new List<Workspace>();

    public void UpdateDisplayName(string displayName, DateTimeOffset now)
    {
        DisplayName = displayName;
        UpdatedAt = now;
    }
}
