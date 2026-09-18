using System.Text.Json;
using System.Text.RegularExpressions;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Persistence;
using ContextDepot.Application.Safety;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Contexts;

public sealed partial class ContextAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceAppService workspaceAppService,
    IContextRepository repository,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    IClock clock) : IContextAppService
{
    public async Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceAppService.ResolveAsync(command.Workspace, cancellationToken)
            ?? throw new ContextDepotApplicationException("WorkspaceNotFound", "The requested workspace does not exist.");
        var content = NormalizeContent(command.Content);
        if (content.Length == 0)
        {
            throw new ContextDepotApplicationException("InvalidContextContent", "Context content is required.");
        }

        Validate(command, content);
        sourceSafety.EnsureSafe(content);
        sourceSafety.EnsureSafe(command.Title);
        sourceSafety.EnsureSafe(command.SourceRef);
        ValidateMetadata(command.MetadataJson);
        var provenance = sourceSafety.EvaluateProvenance(new ProvenanceInput(
            command.SourceType,
            command.VerificationStatus,
            command.RequestedProvenanceTrust,
            command.SourceAgent,
            command.SourceRef));

        var normalizedKey = NormalizeKey(command.Key);
        var previous = normalizedKey is not null
            ? await repository.GetActiveByKeyAsync(currentOwner.OwnerId, workspace.Id, normalizedKey, cancellationToken)
            : null;
        if (previous is not null && previous.Kind != command.Kind)
        {
            throw new ContextDepotApplicationException("ContextKindConflict", "The stable key is already used by another context kind.");
        }

        if (previous is null && command.Kind is ContextKind.Fact or ContextKind.Preference or ContextKind.Decision or ContextKind.Goal && command.SupersedesId is null)
        {
            var duplicate = await repository.GetActiveDuplicateAsync(currentOwner.OwnerId, workspace.Id, command.Kind, content, cancellationToken);
            if (duplicate is not null)
            {
                return new SaveContextResult(ToModel(duplicate), SaveContextOutcome.ReusedExisting);
            }
        }

        if (previous is null && command.Kind == ContextKind.Event && !string.IsNullOrWhiteSpace(command.SourceRef))
        {
            previous = await repository.GetActiveBySourceAsync(currentOwner.OwnerId, workspace.Id, command.SourceType, command.SourceRef.Trim(), cancellationToken);
            if (previous is not null)
            {
                return new SaveContextResult(ToModel(previous), SaveContextOutcome.ReusedExisting);
            }
        }

        ContextItem? explicitTarget = null;
        if (command.SupersedesId is not null)
        {
            if (normalizedKey is not null || command.Kind == ContextKind.State)
            {
                throw new ContextDepotApplicationException("InvalidSupersede", "A stable key and explicit supersedesId cannot be used together.");
            }

            explicitTarget = await repository.GetByIdAsync(currentOwner.OwnerId, command.SupersedesId.Value, cancellationToken);
            if (explicitTarget is null || explicitTarget.WorkspaceId != workspace.Id || explicitTarget.Status != ContextStatus.Active)
            {
                throw new ContextDepotApplicationException("SupersedeTargetNotFound", "The supersede target is not an active context in this workspace.");
            }
        }

        if (previous is not null && command.Kind == ContextKind.Event && string.IsNullOrWhiteSpace(command.SourceRef))
        {
            previous = null;
        }

        if (previous is not null && NormalizeContent(previous.Content) == content && normalizedKey is not null)
        {
            return new SaveContextResult(ToModel(previous), SaveContextOutcome.ReusedExisting);
        }

        var now = clock.UtcNow;
        var context = new ContextItem(idGenerator.NewId(), currentOwner.OwnerId, workspace.Id, command.Kind, normalizedKey, NormalizeOptional(command.Title), content, now);
        context.SetQuality(command.Importance, command.Confidence);
        context.SetValidity(command.ValidFrom, command.ValidUntil, command.ExpiresAt);
        context.SetTags(JsonSerializer.Serialize((command.Tags ?? []).Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        context.SetMetadata(command.MetadataJson);
        context.ConfigureProvenance(provenance.VerificationStatus, provenance.Trust, provenance.SourceType, provenance.SourceAgent, provenance.SourceRef);

        var priorId = previous?.Id ?? explicitTarget?.Id;
        if (previous is not null)
        {
            previous.MarkSuperseded(now);
            context.SetSupersedes(previous.Id);
        }
        else if (explicitTarget is not null)
        {
            explicitTarget.MarkSuperseded(now);
            context.SetSupersedes(explicitTarget.Id);
        }

        await repository.AddAsync(context, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var outcome = priorId is null ? SaveContextOutcome.Created : normalizedKey is null ? SaveContextOutcome.SupersededExisting : SaveContextOutcome.UpdatedCurrentTruth;
        return new SaveContextResult(ToModel(context), outcome, priorId);
    }

    public async Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken)
    {
        var context = await repository.GetByIdAsync(currentOwner.OwnerId, contextId, cancellationToken)
            ?? throw new ContextDepotApplicationException("ContextNotFound", "The requested context does not exist.");
        context.MarkArchived(clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(SaveContextCommand command, string content)
    {
        if (command.Kind == ContextKind.State && string.IsNullOrWhiteSpace(command.Key))
        {
            throw new ContextDepotApplicationException("ContextKeyRequired", "State contexts require a stable key.");
        }

        if (command.Importance is < 0 or > 100 || command.Confidence is < 0 or > 1)
        {
            throw new ContextDepotApplicationException("InvalidContextQuality", "Importance and confidence are out of range.");
        }

        if (content.Length > 100_000)
        {
            throw new ContextDepotApplicationException("ContextTooLarge", "Context content exceeds the maximum size.");
        }
    }

    private static string NormalizeContent(string value) => string.Join(' ', (value ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant();
        if (!KeyRegex().IsMatch(normalized))
        {
            throw new ContextDepotApplicationException("InvalidContextKey", "Context key must contain lowercase letters, numbers, dots, underscores or hyphens.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateMetadata(string metadataJson)
    {
        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException();
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new ContextDepotApplicationException("InvalidContextMetadata", "Context metadata must be a JSON object.");
        }
    }

    private static ContextModel ToModel(ContextItem context)
    {
        var tags = JsonSerializer.Deserialize<string[]>(context.TagsJson) ?? [];
        return new ContextModel(context.Id, context.OwnerId, context.WorkspaceId, context.Kind, context.Key, context.Title, context.Content, tags, context.Importance, context.Confidence, context.Status, context.VerificationStatus, context.ProvenanceTrust, context.SourceType, context.SourceAgent, context.SourceRef, context.SupersedesId, context.ExpiresAt, context.CreatedAt, context.UpdatedAt);
    }

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
