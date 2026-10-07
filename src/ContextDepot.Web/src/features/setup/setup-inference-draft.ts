import type {
  InferenceProviderDraft,
  InferenceProviderSettings,
  InferenceRouteDraft,
  InferenceRoute,
} from "@/features/settings/settings-api";

export interface SetupInferenceDraft {
  settings: InferenceProviderSettings;
  credentials: Map<string, string>;
  newIds: Set<string>;
}
export function createInferenceDraft(
  settings: InferenceProviderSettings,
): SetupInferenceDraft {
  return {
    settings: structuredClone(settings),
    credentials: new Map(),
    newIds: new Set(),
  };
}
export function applySetupProvider(
  current: SetupInferenceDraft,
  draft: InferenceProviderDraft,
): SetupInferenceDraft {
  const settings = structuredClone(current.settings);
  const credentials = new Map(current.credentials),
    newIds = new Set(current.newIds);
  const id = draft.id ?? `draft-provider-${newIds.size + 1}`;
  if (!draft.id) newIds.add(id);
  const previous = settings.providers.find((provider) => provider.id === id);
  if (draft.apiKey.trim()) credentials.set(id, draft.apiKey.trim());
  const provider = {
    id,
    name: draft.name.trim(),
    kind: draft.kind,
    protocol: "openai-compatible",
    endpoint: draft.endpoint.trim(),
    hasApiKey: !!credentials.get(id) || !!previous?.hasApiKey,
    updatedAt: draft.updatedAt ?? settings.routes[0]!.updatedAt,
  };
  settings.providers = [
    ...settings.providers.filter((item) => item.id !== id),
    provider,
  ];
  settings.routes = settings.routes.map((route) => {
    const model = draft[route.capability];
    if (!model.enabled && route.providerId !== id) return route;
    return {
      ...route,
      providerId: model.enabled ? id : null,
      providerName: model.enabled ? provider.name : null,
      endpoint: model.enabled ? provider.endpoint : null,
      hasApiKey: model.enabled && provider.hasApiKey,
      model: model.enabled ? model.model.trim() : null,
      dimensions: model.enabled ? model.dimensions : null,
      timeoutSeconds: model.enabled
        ? model.timeoutSeconds
        : route.timeoutSeconds,
      runtimeState: model.enabled ? "draft" : "unconfigured",
      isApplied: false,
    };
  });
  return { settings, credentials, newIds };
}
export function applySetupRoute(
  current: SetupInferenceDraft,
  capability: InferenceRoute["capability"],
  draft: InferenceRouteDraft,
): SetupInferenceDraft {
  const settings = structuredClone(current.settings);
  const provider = settings.providers.find(
    (item) => item.id === draft.providerId,
  );
  settings.routes = settings.routes.map((route) =>
    route.capability !== capability
      ? route
      : {
          ...route,
          providerId: provider?.id ?? null,
          providerName: provider?.name ?? null,
          endpoint: provider?.endpoint ?? null,
          hasApiKey: provider?.hasApiKey ?? false,
          model: provider ? draft.model.trim() : null,
          dimensions: provider ? draft.dimensions : null,
          timeoutSeconds: draft.timeoutSeconds,
          runtimeState: provider ? "draft" : "unconfigured",
          isApplied: false,
        },
  );
  return { ...current, settings };
}
export function setupProviderRequests(current: SetupInferenceDraft) {
  const embedding = current.settings.routes.find(
    (route) => route.capability === "embedding",
  )!;
  const chat = current.settings.routes.find(
    (route) => route.capability === "chat",
  )!;
  const model = (route: InferenceRoute, id: string) =>
    route.providerId === id
      ? {
          model: route.model,
          dimensions: route.dimensions,
          timeoutSeconds: route.timeoutSeconds,
        }
      : null;
  return current.settings.providers
    .filter((provider) =>
      current.settings.routes.some((route) => route.providerId === provider.id),
    )
    .map((provider) => ({
      id: current.newIds.has(provider.id) ? null : provider.id,
      name: provider.name,
      kind: provider.kind,
      endpoint: provider.endpoint,
      apiKey: current.credentials.get(provider.id) ?? null,
      updatedAt: current.newIds.has(provider.id) ? null : provider.updatedAt,
      embedding: model(embedding, provider.id),
      chat: model(chat, provider.id),
      embeddingUpdatedAt: embedding.updatedAt,
      chatUpdatedAt: chat.updatedAt,
    }));
}
