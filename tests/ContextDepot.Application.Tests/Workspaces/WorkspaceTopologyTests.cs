using ContextDepot.Application.Workspaces;

namespace ContextDepot.Application.Tests.Workspaces;

public sealed class WorkspaceTopologyTests
{
    [Fact]
    public void Paths_and_ancestors_share_one_tree()
    {
        var root = Guid.CreateVersion7();
        var child = Guid.CreateVersion7();
        var leaf = Guid.CreateVersion7();
        var topology = new WorkspaceTopology([
            new WorkspaceTreeNode(leaf, child, "notes"),
            new WorkspaceTreeNode(child, root, "context-depot"),
            new WorkspaceTreeNode(root, null, "projects")
        ]);

        Assert.Equal("projects/context-depot/notes", topology.Paths[leaf]);
        Assert.Equal(new HashSet<Guid> { root, child, leaf }, topology.AncestorsIncludingSelf([leaf]));
        Assert.True(topology.TryGetId("projects/context-depot", out var resolved));
        Assert.Equal(child, resolved);
    }

    [Fact]
    public void Descendants_do_not_cross_a_navigation_only_workspace()
    {
        var root = Guid.CreateVersion7();
        var navigationOnly = Guid.CreateVersion7();
        var grantedLeaf = Guid.CreateVersion7();
        var directChild = Guid.CreateVersion7();
        var topology = new WorkspaceTopology([
            new WorkspaceTreeNode(root, null, "projects"),
            new WorkspaceTreeNode(navigationOnly, root, "private"),
            new WorkspaceTreeNode(grantedLeaf, navigationOnly, "granted"),
            new WorkspaceTreeNode(directChild, root, "public")
        ]);
        var directGrants = new HashSet<Guid> { root, grantedLeaf, directChild };

        Assert.Equal([directChild], topology.AccessibleDescendants(root, directGrants.Contains));
        Assert.Contains(navigationOnly, topology.AncestorsIncludingSelf([grantedLeaf]));
    }
}
