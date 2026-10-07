namespace ContextDepot.Application.Setup.Dtos;

public sealed record SetupStatusDto(string State, SetupWorkspaceDto? Workspace, string NextStep, string McpPath = "/mcp");
