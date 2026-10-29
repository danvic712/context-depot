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
        "Create or update canonical Markdown. Updates require the latest content hash when changed. " +
        "For same-Workspace document links, prefer [label](/known/path.md#heading) using actual saved paths. " +
        "Unsafe link protocols and invalid paths reject the entire write with linkErrors. " +
        "Success returns linkValidation.warnings for missing/archived targets or missing headings; save targets, fix links, " +
        "then document_get the source to recheck. Warnings do not mean the write failed.")]
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
        "Read known canonical Markdown by ID when the full document is needed. " +
        "metadata.linkValidation rechecks current link targets and headings; use it after a related document batch " +
        "or to confirm a link fix. Ordinary tasks should prefer context_bootstrap excerpts.")]
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
    public async Task<ArchiveDocumentResult> ArchiveAsync(IDocumentAppService service, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await executor.ExecuteAsync(async () =>
        {
            await service.ArchiveAsync(documentId, cancellationToken);
            return new ArchiveDocumentResult(true, documentId);
        }, logger);
    }
}
