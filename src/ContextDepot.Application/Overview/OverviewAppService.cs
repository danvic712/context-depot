using System.Text.RegularExpressions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Overview;

public sealed partial class OverviewAppService(
    ICurrentDepotContext currentDepot,
    IOverviewRepository repository,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ISourceSafetyService sourceSafety)
{
    public Task<ResourceCollection<WorkspaceSummary>> ListWorkspacesAsync(int limit, string sort, CancellationToken cancellationToken)
    {
        ValidateQuery(limit, 8, sort, "-activityAt");
        return repository.ListWorkspacesAsync(currentDepot.DepotId, limit, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<ResourceCollection<KnowledgeSummary>> ListKnowledgeAsync(int limit, string sort, CancellationToken cancellationToken)
    {
        ValidateQuery(limit, 20, sort, "-updatedAt");
        return repository.ListKnowledgeAsync(currentDepot.DepotId, limit, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Lists recently updated knowledge within the search page's current conditions.</summary>
    /// <param name="limit">The requested limit, between one and twenty.</param>
    /// <param name="workspace">An optional exact workspace path.</param>
    /// <param name="type">All, contexts or documents.</param>
    /// <param name="kind">An optional context category.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The most recently updated matching knowledge.</returns>
    public Task<ResourceCollection<KnowledgeSummary>> ListRecentKnowledgeAsync(int limit, string? workspace,
        string? type, string? kind, CancellationToken cancellationToken)
    {
        ValidateQuery(limit, 20, "-updatedAt", "-updatedAt");
        type = string.IsNullOrWhiteSpace(type) || type == "all" ? null : type;
        if (type is not null and not "contexts" and not "documents")
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        ContextKind? selectedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<ContextKind>(kind, true, out var parsed) || !Enum.IsDefined(parsed) ||
                !string.Equals(parsed.ToString(), kind, StringComparison.OrdinalIgnoreCase) || type == "documents")
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
            selectedKind = parsed;
        }
        var filter = new RecentKnowledgeFilter(string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim(), type, selectedKind);
        return repository.ListRecentKnowledgeAsync(currentDepot.DepotId, limit, filter, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<WorkspaceSummary> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var path = request.Path?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var errors = new Dictionary<string, string[]>();
        if (name.Length is < 1 or > 200) errors["name"] = [ApplicationErrorCodes.InvalidWorkspaceName];
        if (path.Length is < 1 or > 100 || !RootSlug().IsMatch(path)) errors["path"] = [ApplicationErrorCodes.InvalidWorkspacePath];
        if (errors.Count > 0) throw new ContextDepotApplicationException(errors.First().Value[0], errors);
        sourceSafety.EnsureSafe(name);
        sourceSafety.EnsureSafe(description);
        var id = idGenerator.NewId();
        var now = timeProvider.GetUtcNow();
        if (!await repository.CreateWorkspaceAsync(currentDepot.DepotId, id, name, path, description, now, cancellationToken))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspacePathConflict,
                new Dictionary<string, string[]> { ["path"] = [ApplicationErrorCodes.WorkspacePathConflict] });
        }

        return new WorkspaceSummary(id, name, description, path, 0, 0, now);
    }

    private static void ValidateQuery(int limit, int maximum, string sort, string expected)
    {
        var errors = new Dictionary<string, string[]>();
        if (limit < 1 || limit > maximum) errors["limit"] = [ApplicationErrorCodes.InvalidResourceQuery];
        if (sort != expected) errors["sort"] = [ApplicationErrorCodes.InvalidResourceQuery];
        if (errors.Count > 0) throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery, errors);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex RootSlug();
}
