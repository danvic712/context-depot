import {
  applyInferenceProviderPreset,
  type InferenceProviderDraft,
  type InferenceProviderPreset,
  type InferenceProviderSettings,
  type InferenceRoute,
} from "./settings-api";

export function connectedInferenceProviders(
  settings: InferenceProviderSettings,
) {
  return settings.providers.filter(
    (provider) =>
      provider.hasApiKey ||
      settings.routes.some((route) => route.providerId === provider.id),
  );
}

export function availableInferenceProviders(
  settings: InferenceProviderSettings,
  capability: InferenceRoute["capability"],
) {
  return settings.providers.filter(
    (provider) =>
      provider.hasApiKey &&
      provider.endpoint &&
      (capability === "chat" ||
        settings.presets.find((preset) => preset.kind === provider.kind)
          ?.supportsEmbedding),
  );
}

export function connectInferenceProviderDraft(
  draft: InferenceProviderDraft,
  preset: InferenceProviderPreset,
  settings: InferenceProviderSettings,
  capability?: InferenceRoute["capability"],
): InferenceProviderDraft {
  const existing = settings.providers.find(
    (provider) =>
      provider.kind === preset.kind &&
      !provider.hasApiKey &&
      !settings.routes.some((route) => route.providerId === provider.id),
  );
  const next = applyInferenceProviderPreset(draft, preset);
  return {
    ...next,
    id: existing?.id ?? null,
    updatedAt: existing?.updatedAt ?? null,
    name: existing?.name ?? next.name,
    endpoint: existing?.endpoint ?? next.endpoint,
    embedding: {
      ...next.embedding,
      enabled: capability === "embedding" && preset.supportsEmbedding,
    },
    chat: { ...next.chat, enabled: capability === "chat" },
  };
}
