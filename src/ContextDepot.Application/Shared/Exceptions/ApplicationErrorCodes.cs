namespace ContextDepot.Application.Shared.Exceptions;

public static class ApplicationErrorCodes
{
    public const string InternalError = "InternalError";
    public const string OwnerNotConfigured = "OwnerNotConfigured";
    public const string OwnerConfigurationMismatch = "OwnerConfigurationMismatch";
    public const string ContextNotFound = "ContextNotFound";
    public const string ContextConcurrencyConflict = "ContextConcurrencyConflict";
    public const string ContextWriteFailed = "ContextWriteFailed";
    public const string ContextKindConflict = "ContextKindConflict";
    public const string InvalidContextContent = "InvalidContextContent";
    public const string InvalidContextKind = "InvalidContextKind";
    public const string InvalidContextKey = "InvalidContextKey";
    public const string InvalidContextMetadata = "InvalidContextMetadata";
    public const string InvalidContextQuality = "InvalidContextQuality";
    public const string InvalidStateKey = "InvalidStateKey";
    public const string InvalidSupersedeTarget = "InvalidSupersedeTarget";
    public const string InvalidVerificationStatus = "InvalidVerificationStatus";
    public const string SecretContentRejected = "SecretContentRejected";
    public const string ContextTooLarge = "ContextTooLarge";
    public const string WorkspaceNotFound = "WorkspaceNotFound";
    public const string WorkspaceParentNotFound = "WorkspaceParentNotFound";
    public const string WorkspaceConcurrencyConflict = "WorkspaceConcurrencyConflict";
    public const string WorkspaceWriteFailed = "WorkspaceWriteFailed";
    public const string InvalidWorkspaceName = "InvalidWorkspaceName";
    public const string InvalidWorkspaceMetadata = "InvalidWorkspaceMetadata";
    public const string InvalidWorkspacePath = "InvalidWorkspacePath";
    public const string DocumentNotFound = "DocumentNotFound";
    public const string DocumentConflict = "DocumentConflict";
    public const string InvalidDocumentTitle = "InvalidDocumentTitle";
    public const string InvalidDocumentPath = "InvalidDocumentPath";
    public const string DocumentWriteFailed = "DocumentWriteFailed";
    public const string ContextBudgetInvalid = "ContextBudgetInvalid";
    public const string DatabaseMigrationFailed = "DatabaseMigrationFailed";
    public const string DatabaseUnavailable = "DatabaseUnavailable";
    public const string DesignTimeConfigurationMissing = "DesignTimeConfigurationMissing";
    public const string MarkdownRootUnavailable = "MarkdownRootUnavailable";
    public const string EmbeddingConfigurationInvalid = "EmbeddingConfigurationInvalid";
    public const string EmbeddingGeneratorUnavailable = "EmbeddingGeneratorUnavailable";
    public const string EmbeddingGeneratorInvalidResponse = "EmbeddingGeneratorInvalidResponse";
    public const string EmbeddingDimensionMismatch = "EmbeddingDimensionMismatch";
    public const string MarkdownFileMissing = "MarkdownFileMissing";
    public const string IndexRepairRequestInvalid = "IndexRepairRequestInvalid";
}
