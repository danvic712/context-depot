using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceUpsertPersistenceResult(
    Workspace? Workspace,
    WorkspaceUpsertPersistenceOutcome Outcome);
