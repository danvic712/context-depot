using ContextDepot.Domain.Workspaces;
using System.Text.Json.Serialization;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceSubject([property: JsonRequired] WorkspaceSubjectType Type, [property: JsonRequired] string Key);
