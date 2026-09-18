using System.ComponentModel;
using ContextDepot.Application.Documents;
using ModelContextProtocol.Server;

namespace ContextDepot.Mcp;

public sealed class DocumentTools
{
    [McpServerTool(Name = "document_upsert", Title = "Write canonical Markdown", Destructive = false, UseStructuredContent = true)]
    [Description("Create or update a canonical Markdown document. Use this for durable designs, ADRs, runbooks, research, and plans. Updates require the latest content hash when the file changed.")]
    public async Task<DocumentModel> UpsertAsync(
        IDocumentAppService service,
        string workspace,
        string path,
        string title,
        string content,
        string? expectedContentHash = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await service.UpsertAsync(new UpsertDocumentCommand(workspace, path, title, content, expectedContentHash), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }

    [McpServerTool(Name = "document_get", Title = "Read canonical Markdown", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Read a known canonical Markdown document by ID when the full document is explicitly needed. Ordinary tasks should prefer context_bootstrap excerpts.")]
    public async Task<DocumentContentModel?> GetAsync(IDocumentAppService service, Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await service.GetAsync(documentId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }

    [McpServerTool(Name = "document_archive", Title = "Archive Markdown", Destructive = true, UseStructuredContent = true)]
    [Description("Archive a known Markdown document in the current owner scope without deleting its canonical file.")]
    public async Task<object> ArchiveAsync(IDocumentAppService service, Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await service.ArchiveAsync(documentId, cancellationToken);
            return new { archived = true, documentId };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            McpToolErrorMapper.Throw(exception);
            throw;
        }
    }
}
