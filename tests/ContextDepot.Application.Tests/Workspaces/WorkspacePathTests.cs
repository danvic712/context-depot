using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Tests.Workspaces;

public sealed class WorkspacePathTests
{
    [Fact]
    public void BuildPaths_orders_nested_workspace_slugs_from_root_to_leaf()
    {
        var depotId = Guid.CreateVersion7();
        var root = new Workspace(Guid.CreateVersion7(), depotId, null, "Projects", "projects", null, DateTimeOffset.UtcNow);
        var child = new Workspace(Guid.CreateVersion7(), depotId, root.Id, "Context Depot", "context-depot", null, DateTimeOffset.UtcNow);
        var leaf = new Workspace(Guid.CreateVersion7(), depotId, child.Id, "Notes", "notes", null, DateTimeOffset.UtcNow);

        var paths = WorkspacePath.BuildPaths([leaf, child, root]);

        Assert.Equal("projects", paths[root.Id]);
        Assert.Equal("projects/context-depot", paths[child.Id]);
        Assert.Equal("projects/context-depot/notes", paths[leaf.Id]);
        Assert.Equal("projects/context-depot/notes", WorkspacePath.BuildPath(
            new Dictionary<Guid, Workspace> { [root.Id] = root, [child.Id] = child, [leaf.Id] = leaf },
            leaf));
    }
}
