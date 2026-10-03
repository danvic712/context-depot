namespace ContextDepot.Domain.Inferences;

public static class InferenceProviderKinds
{
    public const string Custom = "custom";
    public const string OpenAI = "openai";
    public const string AzureOpenAI = "azure-openai";
    public const string DeepSeek = "deepseek";

    public static bool IsSupported(string kind) => kind is Custom or OpenAI or AzureOpenAI or DeepSeek;
    public static bool SupportsEmbedding(string kind) => IsSupported(kind) && kind != DeepSeek;

    public static bool IsValidEndpoint(string kind, Uri endpoint) => kind != AzureOpenAI ||
        endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase);
}
