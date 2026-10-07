using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Setup.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Setup;

public sealed class SetupAppService(ISetupRepository repository, IIdGenerator ids, TimeProvider clock,
    IOptions<SetupOptions> options, ISourceSafetyService sourceSafety, ISecretProtector protector,
    IAccessKeySecretGenerator keyGenerator) : ISetupAppService
{
    public async Task<SetupStatusDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await repository.GetAsync(cancellationToken));

    public Task<InferenceProviderSettingsDto> GetInferenceSettingsAsync(CancellationToken cancellationToken) =>
        repository.GetInferenceSettingsAsync(cancellationToken);

    public async Task<SetupCompletionDto> CompleteAsync(CompleteSetupRequest request, CancellationToken cancellationToken)
    {
        var workspaceDraft = ValidateWorkspace(request.Workspace);
        if (request.Providers is null || request.Providers.Count > 20 ||
            request.Providers.Any(provider => provider is null) ||
            request.AccessKeyName is not null && (string.IsNullOrWhiteSpace(request.AccessKeyName) || request.AccessKeyName.Trim().Length > 200))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest);
        var providers = request.Providers.Select(InferenceConfigurationValidator.NormalizeProvider).ToArray();
        if (providers.Count(provider => provider.Embedding is not null) > 1 || providers.Count(provider => provider.Chat is not null) > 1 ||
            providers.Where(provider => provider.Id is not null).Select(provider => provider.Id).Distinct().Count() != providers.Count(provider => provider.Id is not null))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
        var keyName = request.AccessKeyName?.Trim();
        GeneratedAccessKey? generated = null;
        DepotAccessKey? accessKey = null;
        var snapshot = await repository.MutateAsync(current =>
        {
            // A lost completion response must never create another Workspace or credential.
            if (current.Installation.State == InstallationState.Completed) return null;
            var now = clock.GetUtcNow();
            var existing = current.Installation.State == InstallationState.InProgress;
            Workspace workspace;
            Depot depot;
            if (existing)
            {
                EnsureInitialResources(current);
                workspace = current.Workspace!;
                workspace.UpdateDetails(workspaceDraft.Name, workspaceDraft.Path, workspaceDraft.Description, now);
                depot = new Depot(workspace.DepotId, "ContextDepot", now);
            }
            else
            {
                if (current.HasAnyDepot) throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupConflict);
                depot = new Depot(NewDepotId(), "ContextDepot", now);
                workspace = new Workspace(ids.NewId(), depot.Id, null, workspaceDraft.Name, workspaceDraft.Path, workspaceDraft.Description, now);
            }
            var configuredProviders = new List<InferenceProvider>();
            foreach (var draft in providers)
            {
                var provider = draft.Id is Guid id ? current.Providers.SingleOrDefault(item => item.Id == id) : null;
                if (draft.Id is not null && (provider is null || provider.UpdatedAt != draft.UpdatedAt))
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
                if (provider is not null && provider.Kind != draft.Kind)
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
                if (current.Routes.Any(route => route.Capability == "embedding" && route.UpdatedAt != draft.EmbeddingUpdatedAt ||
                    route.Capability == "chat" && route.UpdatedAt != draft.ChatUpdatedAt))
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
                var protectedKey = draft.ApiKey is null ? provider?.ProtectedApiKey
                    : protector.Protect(draft.ApiKey, SecretProtectionPurpose.InferenceProviderApiKey);
                if (string.IsNullOrWhiteSpace(protectedKey) ||
                    !protector.TryUnprotect(protectedKey, SecretProtectionPurpose.InferenceProviderApiKey, out var key) || string.IsNullOrWhiteSpace(key))
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
                provider ??= new InferenceProvider { Id = ids.NewId(), CreatedAt = now };
                provider.Name = draft.Name; provider.Kind = draft.Kind; provider.ProtocolCode = "openai-compatible";
                provider.BaseUrl = draft.Endpoint; provider.ProtectedApiKey = protectedKey; provider.VerificationState = "unverified"; provider.UpdatedAt = now;
                configuredProviders.Add(provider);
                AssignModel(current.Routes.Single(route => route.Capability == "embedding"), draft.Embedding, provider, now);
                AssignModel(current.Routes.Single(route => route.Capability == "chat"), draft.Chat, provider, now);
            }
            // Unselected capabilities stay unconfigured, including when a legacy draft is cleared.
            foreach (var route in current.Routes)
            {
                if (!providers.Any(provider => route.Capability == "embedding" ? provider.Embedding is not null : provider.Chat is not null))
                {
                    route.Provider = null; route.ProviderId = null; route.ModelName = null; route.Dimensions = null; route.UpdatedAt = now;
                }
            }
            if (keyName is not null)
            {
                generated = keyGenerator.Generate();
                accessKey = new DepotAccessKey(ids.NewId(), depot.Id, keyName, generated.Prefix, generated.SecretHash, now);
                accessKey.WorkspaceGrants.Add(new WorkspaceAccessGrant(accessKey.Id, depot.Id, workspace.Id, now));
            }
            current.Installation.Begin(depot.Id, workspace.Id, now);
            current.Installation.ReviewInference(now);
            current.Installation.ReviewAccessKey(now);
            current.Installation.Complete(now);
            return new SetupResources(depot, workspace)
            {
                ExistingWorkspace = existing, Providers = configuredProviders, Routes = current.Routes, AccessKey = accessKey
            };
        }, cancellationToken);
        var issued = generated is not null && accessKey is not null ? new IssuedAccessKeyDto(
            new AccessKeyDto(accessKey.Id, accessKey.Name, accessKey.KeyPrefix, accessKey.CreatedAt, null, null, [snapshot.Workspace!.Id]), generated.Plaintext) : null;
        return new(ToDto(snapshot), issued);
    }

    private static void AssignModel(InferenceRoute route, InferenceProviderModelRequest? model, InferenceProvider provider, DateTimeOffset now)
    {
        if (model is null) return;
        route.Provider = provider; route.ProviderId = provider.Id; route.ModelName = model.Model;
        route.Dimensions = model.Dimensions; route.TimeoutSeconds = model.TimeoutSeconds; route.UpdatedAt = now;
    }

    private SetupWorkspaceRequest ValidateWorkspace(SetupWorkspaceRequest? request)
    {
        var name = request?.Name?.Trim(); var path = request?.Path?.Trim();
        var description = string.IsNullOrWhiteSpace(request?.Description) ? null : request.Description.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || string.IsNullOrEmpty(path) || path.Length > 100 || path.Contains('/') || description?.Length > 2000)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSetupWorkspace);
        try { WorkspacePath.Normalize(path); }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode == ApplicationErrorCodes.InvalidWorkspacePath)
        { throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSetupWorkspace); }
        sourceSafety.EnsureSafe(name); sourceSafety.EnsureSafe(description);
        return new(name, path, description);
    }

    private Guid NewDepotId()
    {
        if (string.IsNullOrWhiteSpace(options.Value.DepotId)) return ids.NewId();
        if (!Guid.TryParse(options.Value.DepotId, out var id) || id.Version != 7)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupUnavailable);
        return id;
    }

    private void EnsureInitialResources(SetupSnapshot snapshot)
    {
        if (!snapshot.InitialDepotExists || snapshot.Workspace is not { } workspace ||
            workspace.Id != snapshot.Installation.InitialWorkspaceId || workspace.DepotId != snapshot.Installation.InitialDepotId || workspace.ParentWorkspaceId is not null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupUnavailable);
        var configured = options.Value.DepotId;
        if (!string.IsNullOrWhiteSpace(configured) && (!Guid.TryParse(configured, out var id) || id != snapshot.Installation.InitialDepotId))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupUnavailable);
    }

    private static SetupStatusDto ToDto(SetupSnapshot snapshot)
    {
        var state = snapshot.Installation;
        var workspace = snapshot.Workspace is { } initial && initial.Id == state.InitialWorkspaceId && initial.DepotId == state.InitialDepotId
            ? new SetupWorkspaceDto(initial.Id, initial.Name, initial.Slug, initial.Description) : null;
        // Legacy partial setups can recover their recorded space, but all new progress lives in the browser draft.
        return new(state.State, workspace, state.State == InstallationState.Completed ? "review" : "workspace");
    }
}
