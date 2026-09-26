using System.ComponentModel;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Tools;

public sealed class DocumentTools(MCPToolExecutor executor, ILogger<DocumentTools> logger)
{
    [McpServerTool(Name = "document_upsert", Title = "Write canonical Markdown", Destructive = false,
        UseStructuredContent = true)]
    [Description(
        "Create or update a canonical Markdown document. Use this for durable designs, ADRs, runbooks, research, and plans. Updates require the latest content hash when the file changed.")]
    public async Task<DocumentModel> UpsertAsync(
        IDocumentAppService service,
        string workspace,
        string path,
        string title,
        string content,
        string? expectedContentHash = null,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(
            () => service.UpsertAsync(new UpsertDocumentCommand(workspace, path, title, content, expectedContentHash),
                cancellationToken),
            logger);
    }

    [McpServerTool(Name = "document_get", Title = "Read canonical Markdown", ReadOnly = true, Idempotent = true,
        UseStructuredContent = true)]
    [Description(
        "Read a known canonical Markdown document by ID when the full document is explicitly needed. Ordinary tasks should prefer context_bootstrap excerpts.")]
    public async Task<DocumentContentModel?> GetAsync(IDocumentAppService service, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(
            () => service.GetAsync(documentId, cancellationToken),
            logger);
    }

    [McpServerTool(Name = "document_archive", Title = "Archive Markdown", Destructive = true,
        UseStructuredContent = true)]
    [Description("Archive a known Markdown document in the current depot scope without deleting its canonical file.")]
    public async Task<object> ArchiveAsync(IDocumentAppService service, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(async () =>
        {
            await service.ArchiveAsync(documentId, cancellationToken);
            return (object)new { archived = true, documentId };
        }, logger);
    }
}