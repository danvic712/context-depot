using ContextDepot.Application.Retrieval.Enums;
using SearchMatchType = ContextDepot.Application.Retrieval.Enums.MatchType;

namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentSearchMatch(
    Guid DocumentId,
    string Workspace,
    string Path,
    string Title,
    string HeadingPath,
    string Excerpt,
    SearchMatchType MatchType);
