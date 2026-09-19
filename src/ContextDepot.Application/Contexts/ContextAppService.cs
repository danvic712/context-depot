using System.Text.Json;
using System.Text.RegularExpressions;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Shared.Validation;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Contexts;

public sealed partial class ContextAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceAppService workspaceAppService,
    IContextRepository repository,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    TimeProvider timeProvider) : IContextAppService
{
    public async Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceAppService.ResolveAsync(command.Workspace, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        var content = NormalizeContent(command.Content);
        if (content.Length == 0)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextContent);
        }

        Validate(command, content);
        sourceSafety.EnsureSafe(workspace.Path);
        sourceSafety.EnsureSafe(content);
        sourceSafety.EnsureSafe(command.Title);
        sourceSafety.EnsureSafe(command.Key);
        sourceSafety.EnsureSafe(command.SourceAgent);
        sourceSafety.EnsureSafe(command.SourceRef);
        sourceSafety.EnsureSafe(command.MetadataJson);
        foreach (var tag in command.Tags ?? [])
        {
            sourceSafety.EnsureSafe(tag);
        }

        JsonObjectValidator.EnsureObject(command.MetadataJson, ApplicationErrorCodes.InvalidContextMetadata);
        var provenance = sourceSafety.EvaluateProvenance(new ProvenanceInput(
            command.SourceType,
            command.VerificationStatus,
            command.RequestedProvenanceTrust,
            command.SourceAgent,
            command.SourceRef,
            command.IsTrustedServer));

        var normalizedKey = NormalizeKey(command.Key);
        if (command.SupersedesId is not null && (normalizedKey is not null || command.Kind == ContextKind.State))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSupersedeTarget);
        }

        var now = timeProvider.GetUtcNow();
        var context = new ContextItem(
            idGenerator.NewId(),
            currentOwner.OwnerId,
            workspace.Id,
            command.Kind,
            normalizedKey,
            NormalizeOptional(command.Title),
            content,
            now);
        context.SetQuality(command.Importance, command.Confidence);
        context.SetValidity(command.ValidFrom, command.ValidUntil, command.ExpiresAt);
        context.SetTags(JsonSerializer.Serialize((command.Tags ?? []).Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        context.SetMetadata(command.MetadataJson);
        context.ConfigureProvenance(
            provenance.VerificationStatus,
            provenance.Trust,
            provenance.SourceType,
            NormalizeOptional(provenance.SourceAgent),
            NormalizeOptional(provenance.SourceRef));

        ContextPersistenceResult result;
        if (command.SupersedesId is Guid supersedesId)
        {
            result = await repository.SupersedeByIdAsync(context, supersedesId, now, cancellationToken);
        }
        else if (normalizedKey is not null)
        {
            result = await repository.SaveKeyedAsync(context, now, cancellationToken);
        }
        else
        {
            var duplicatePolicy = command.Kind switch
            {
                ContextKind.Fact or ContextKind.Preference or ContextKind.Decision or ContextKind.Goal => UnkeyedDuplicatePolicy.ExactContent,
                ContextKind.Event when !string.IsNullOrWhiteSpace(context.SourceRef) => UnkeyedDuplicatePolicy.SourceIdentity,
                _ => UnkeyedDuplicatePolicy.None
            };
            result = await repository.SaveUnkeyedAsync(context, duplicatePolicy, now, cancellationToken);
        }

        return result.Outcome switch
        {
            ContextPersistenceOutcome.Created => ToSaveResult(result, SaveContextOutcome.Created),
            ContextPersistenceOutcome.Replaced => ToSaveResult(result, result.PreviousContextId is not null && normalizedKey is not null ? SaveContextOutcome.UpdatedCurrentTruth : SaveContextOutcome.SupersededExisting),
            ContextPersistenceOutcome.ReusedExisting => ToSaveResult(result, SaveContextOutcome.ReusedExisting),
            ContextPersistenceOutcome.KindConflict => throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextKindConflict),
            ContextPersistenceOutcome.InvalidTarget => throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSupersedeTarget),
            ContextPersistenceOutcome.ConcurrencyConflict => throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextConcurrencyConflict),
            _ => throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextWriteFailed)
        };
    }

    public async Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken)
    {
        var result = await repository.ArchiveAsync(currentOwner.OwnerId, contextId, timeProvider.GetUtcNow(), cancellationToken);
        if (result.Outcome == ContextPersistenceOutcome.NotFound)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextNotFound);
        }

        if (result.Outcome == ContextPersistenceOutcome.ConcurrencyConflict)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextConcurrencyConflict);
        }
    }

    private static SaveContextResult ToSaveResult(ContextPersistenceResult result, SaveContextOutcome outcome)
    {
        if (result.Context is null)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextWriteFailed);
        }

        return new SaveContextResult(ToModel(result.Context), outcome, result.PreviousContextId);
    }

    private static void Validate(SaveContextCommand command, string content)
    {
        if (command.Kind == ContextKind.Observation)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKind);
        }

        if (command.Kind == ContextKind.State && string.IsNullOrWhiteSpace(command.Key))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidStateKey);
        }

        if (command.Importance is < 0 or > 100 || command.Confidence is < 0 or > 1)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextQuality);
        }

        if (content.Length > 100_000)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextTooLarge);
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
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidContextKey);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ContextModel ToModel(ContextItem context)
    {
        var tags = JsonSerializer.Deserialize<string[]>(context.TagsJson) ?? [];
        return new ContextModel(context.Id, context.OwnerId, context.WorkspaceId, context.Kind, context.Key, context.Title, context.Content, tags, context.Importance, context.Confidence, context.Status, context.VerificationStatus, context.ProvenanceTrust, context.SourceType, context.SourceAgent, context.SourceRef, context.SupersedesId, context.ExpiresAt, context.CreatedAt, context.UpdatedAt);
    }

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
