using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Overview;

/// <summary>
/// Describes a context or document listed in an overview or workspace.
/// </summary>
/// <param name="Id">
/// The context or document identifier.
/// </param>
/// <param name="Type">
/// The resource type: context or document.
/// </param>
/// <param name="Kind">
/// The context kind, or null for documents.
/// </param>
/// <param name="Title">
/// The resource display title.
/// </param>
/// <param name="Workspace">
/// The identifier and full path of the owning workspace.
/// </param>
/// <param name="UpdatedAt">
/// The most recent resource update time.
/// </param>
/// <param name="IndexStatus">
/// The document indexing status, or null for contexts.
/// </param>
public sealed record KnowledgeSummary(Guid Id, string Type, ContextKind? Kind, string Title,
    KnowledgeWorkspace Workspace, DateTimeOffset UpdatedAt, DocumentIndexStatus? IndexStatus);
