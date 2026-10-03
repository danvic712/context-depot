using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Domain.Inferences;

namespace ContextDepot.Application.Settings;

public static class InferenceProviderCatalog
{
    public static IReadOnlyList<InferenceProviderPresetDto> Presets { get; } = Array.AsReadOnly<InferenceProviderPresetDto>([
        new(InferenceProviderKinds.OpenAI, "OpenAI", "https://api.openai.com/v1/", "https://api.openai.com/v1/", true),
        new(InferenceProviderKinds.AzureOpenAI, "Azure OpenAI", null, "https://YOUR-RESOURCE-NAME.openai.azure.com/openai/v1/", true),
        new(InferenceProviderKinds.DeepSeek, "DeepSeek", "https://api.deepseek.com/v1/", "https://api.deepseek.com/v1/", false),
        new(InferenceProviderKinds.Custom, "Custom", null, "https://example.com/v1/", true)
    ]);
}
