using ContextDepot.Application.Embeddings.Dtos;

namespace ContextDepot.Application.Tests.Embeddings;

public sealed class VectorCoverageSnapshotTests
{
    [Fact]
    public void Coverage_is_calculated_independently_for_contexts_and_documents()
    {
        var snapshot = new VectorCoverageSnapshot(4, 3, 2, 1);

        Assert.Equal(0.75, snapshot.ContextCoverage);
        Assert.Equal(0.5, snapshot.DocumentCoverage);
        Assert.False(snapshot.IsComplete);
    }

    [Fact]
    public void Empty_source_sets_are_complete_until_items_need_indexing()
    {
        var snapshot = new VectorCoverageSnapshot(0, 0, 0, 0);

        Assert.Equal(1, snapshot.ContextCoverage);
        Assert.Equal(1, snapshot.DocumentCoverage);
        Assert.True(snapshot.IsComplete);
    }
}
