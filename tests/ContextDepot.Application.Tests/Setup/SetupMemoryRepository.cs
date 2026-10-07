using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Setup;

namespace ContextDepot.Application.Tests.Setup;

internal sealed class SetupMemoryRepository : ISetupRepository
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-04T12:00:00Z");
    public SetupSnapshot Snapshot { get; set; } = new(new InstallationState(Guid.CreateVersion7(), Now), null, false, false)
    {
        Routes = [new InferenceRoute { Id = Guid.CreateVersion7(), Capability = "embedding", CreatedAt = Now, UpdatedAt = Now, TimeoutSeconds = 30 },
            new InferenceRoute { Id = Guid.CreateVersion7(), Capability = "chat", CreatedAt = Now, UpdatedAt = Now, TimeoutSeconds = 30 }]
    };
    public SetupResources? Created { get; private set; }
    public int CreatedCount { get; private set; }
    public int MutationCount { get; private set; }
    public Task<InferenceProviderSettingsDto> GetInferenceSettingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new InferenceProviderSettingsDto([], Snapshot.Routes.Select(route => new InferenceRouteDto(route.Capability,
            null, "openai-compatible", null, null, null, 30, false, route.UpdatedAt, "unconfigured", "unconfigured", false)).ToArray()));
    public Task<SetupSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);
    public Task<SetupSnapshot> MutateAsync(Func<SetupSnapshot, SetupResources?> mutation, CancellationToken cancellationToken)
    {
        MutationCount++;
        var resources = mutation(Snapshot);
        if (resources is not null)
        {
            Created = resources; CreatedCount++;
            Snapshot = Snapshot with { Workspace = resources.Workspace, InitialDepotExists = true, HasAnyDepot = true };
        }
        return Task.FromResult(Snapshot);
    }
}
