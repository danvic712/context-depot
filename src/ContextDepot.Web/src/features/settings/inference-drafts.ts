import { connectInferenceProviderDraft } from "./inference-connections";
import type {
  InferenceProvider,
  InferenceProviderDraft,
  InferenceProviderSettings,
  InferenceRoute,
  InferenceRouteDraft,
} from "./settings-api";

export function inferenceRouteDraft(
  route: InferenceRoute,
  settings: InferenceProviderSettings,
): InferenceRouteDraft {
  return {
    providerId: route.providerId,
    providerUpdatedAt:
      settings.providers.find((provider) => provider.id === route.providerId)
        ?.updatedAt ?? null,
    model: route.model ?? "",
    dimensions: route.dimensions,
    timeoutSeconds: route.timeoutSeconds,
    updatedAt: route.updatedAt,
  };
}

export function inferenceProviderDraft(
  provider: InferenceProvider | undefined,
  settings: InferenceProviderSettings,
  capability?: InferenceRoute["capability"],
): InferenceProviderDraft {
  const embedding = settings.routes.find(
    (route) => route.capability === "embedding",
  )!;
  const chat = settings.routes.find((route) => route.capability === "chat")!;
  const preset = settings.presets.find((preset) => preset.kind === "openai")!;
  function modelDraft(route: InferenceRoute) {
    const selected = !!provider && route.providerId === provider.id;
    return {
      enabled: selected,
      model: selected ? (route.model ?? "") : "",
      dimensions:
        selected && route.capability === "embedding" ? route.dimensions : null,
      timeoutSeconds: selected ? route.timeoutSeconds : 30,
    };
  }
  const initial: InferenceProviderDraft = {
    id: provider?.id ?? null,
    kind: provider?.kind ?? "openai",
    name: provider?.name ?? "OpenAI",
    endpoint: provider?.endpoint ?? (provider ? "" : (preset.endpoint ?? "")),
    apiKey: "",
    updatedAt: provider?.updatedAt ?? null,
    embedding: modelDraft(embedding),
    chat: modelDraft(chat),
    embeddingUpdatedAt: embedding.updatedAt,
    chatUpdatedAt: chat.updatedAt,
  };
  return provider
    ? initial
    : connectInferenceProviderDraft(initial, preset, settings, capability);
}
