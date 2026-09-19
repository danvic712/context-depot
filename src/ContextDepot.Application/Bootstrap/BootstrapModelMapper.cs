using System.Text.Json;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Bootstrap;

internal static class BootstrapModelMapper
{
    public static ContextModel ToContextModel(BootstrapContextCandidate context)
    {
        var tags = JsonSerializer.Deserialize<string[]>(context.TagsJson) ?? [];
        return new ContextModel(
            context.Id,
            context.OwnerId,
            context.WorkspaceId,
            context.Kind,
            context.Key,
            context.Title,
            context.Content,
            tags,
            context.Importance,
            context.Confidence,
            context.Status,
            context.VerificationStatus,
            context.ProvenanceTrust,
            context.SourceType,
            context.SourceAgent,
            context.SourceRef,
            context.SupersedesId,
            context.ExpiresAt,
            context.CreatedAt,
            context.UpdatedAt);
    }

    public static DocumentExcerptModel ToDocumentExcerpt(
        BootstrapDocumentChunkCandidate chunk,
        IReadOnlyDictionary<Guid, string> workspacePaths)
    {
        var workspacePath = workspacePaths.GetValueOrDefault(chunk.WorkspaceId, string.Empty);
        var path = workspacePath.Length == 0 ? chunk.Path : workspacePath + "/" + chunk.Path;
        return new DocumentExcerptModel(chunk.DocumentId, chunk.Id, path, chunk.HeadingPath, chunk.Content, chunk.ContentHash);
    }
}
