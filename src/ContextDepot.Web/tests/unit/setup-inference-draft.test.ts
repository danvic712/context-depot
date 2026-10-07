import { describe, expect, test } from "bun:test";
import {
  createInferenceDraft,
  applySetupProvider,
  applySetupRoute,
  setupProviderRequests,
} from "../../src/features/setup/setup-inference-draft";
import { inferenceProviderDraft } from "../../src/features/settings/inference-drafts";
import type { InferenceProviderSettings } from "../../src/features/settings/settings-api";

const now = "2026-10-07T00:00:00Z";
const settings: InferenceProviderSettings = {
  providers: [],
  presets: [
    {
      kind: "custom",
      name: "Custom",
      endpoint: null,
      endpointPlaceholder: "https://example.test/v1/",
      supportsEmbedding: true,
    },
    {
      kind: "openai",
      name: "OpenAI",
      endpoint: "https://api.openai.com/v1/",
      endpointPlaceholder: "",
      supportsEmbedding: true,
    },
  ],
  routes: ["embedding", "chat"].map((capability) => ({
    capability: capability as "embedding" | "chat",
    providerId: null,
    providerName: null,
    protocol: "openai-compatible",
    endpoint: null,
    model: null,
    dimensions: null,
    timeoutSeconds: 30,
    hasApiKey: false,
    updatedAt: now,
    runtimeState: "unconfigured",
    indexState: "unconfigured",
    isApplied: false,
  })),
};

describe("Setup model drafts", () => {
  test("editing retains only the latest credential in the final request and never returns it as display metadata", () => {
    let draft = createInferenceDraft(settings);
    const first = {
      ...inferenceProviderDraft(undefined, settings, "embedding"),
      apiKey: "fixture-first",
      embedding: {
        enabled: true,
        model: "embed",
        dimensions: 8,
        timeoutSeconds: 30,
      },
    };
    draft = applySetupProvider(draft, first);
    const provider = draft.settings.providers[0]!;
    draft = applySetupProvider(draft, {
      ...first,
      id: provider.id,
      updatedAt: provider.updatedAt,
      apiKey: "fixture-final",
    });
    expect(JSON.stringify(draft.settings)).not.toContain("fixture-first");
    expect(JSON.stringify(draft.settings)).not.toContain("fixture-final");
    const request = setupProviderRequests(draft);
    expect(request).toHaveLength(1);
    expect(request[0]!.apiKey).toBe("fixture-final");
    expect(request[0]!.id).toBeNull();
    expect(JSON.stringify(request)).not.toContain("fixture-first");
    expect(settings.providers).toHaveLength(0);
  });
  test("independent routes keep their selected models and unused intermediate providers are not submitted", () => {
    let draft = createInferenceDraft(settings);
    const initial = inferenceProviderDraft(undefined, settings, "embedding");
    draft = applySetupProvider(draft, {
      ...initial,
      apiKey: "fixture-embedding",
      embedding: {
        enabled: true,
        model: "embed",
        dimensions: 8,
        timeoutSeconds: 30,
      },
    });
    const embeddingId = draft.settings.routes[0]!.providerId;
    draft = applySetupProvider(draft, {
      ...initial,
      name: "Chat service",
      apiKey: "fixture-chat",
      embedding: { ...initial.embedding, enabled: false },
      chat: {
        enabled: true,
        model: "chat",
        dimensions: null,
        timeoutSeconds: 20,
      },
    });
    expect(draft.settings.routes[0]!.providerId).toBe(embeddingId);
    expect(setupProviderRequests(draft)).toHaveLength(2);
    const route = draft.settings.routes[0]!;
    draft = applySetupRoute(draft, "embedding", {
      providerId: null,
      providerUpdatedAt: null,
      model: "",
      dimensions: null,
      timeoutSeconds: 30,
      updatedAt: route.updatedAt,
    });
    expect(setupProviderRequests(draft)).toHaveLength(1);
    expect(setupProviderRequests(draft)[0]!.name).toBe("Chat service");
    expect(JSON.stringify(setupProviderRequests(draft))).not.toContain(
      "fixture-embedding",
    );
  });
  test("leaving a draft key blank preserves it while switching the selected provider retains route revisions", () => {
    let draft = createInferenceDraft(settings);
    const first = {
      ...inferenceProviderDraft(undefined, settings, "embedding"),
      apiKey: "fixture-retained",
      embedding: {
        enabled: true,
        model: "old",
        dimensions: 8,
        timeoutSeconds: 30,
      },
    };
    draft = applySetupProvider(draft, first);
    const provider = draft.settings.providers[0]!;
    draft = applySetupProvider(draft, {
      ...first,
      id: provider.id,
      apiKey: "",
      updatedAt: provider.updatedAt,
      embedding: { ...first.embedding, model: "latest" },
    });
    const request = setupProviderRequests(draft)[0]!;
    expect(request.apiKey).toBe("fixture-retained");
    expect(request.embedding?.model).toBe("latest");
    expect(request.embeddingUpdatedAt).toBe(now);
  });
});
