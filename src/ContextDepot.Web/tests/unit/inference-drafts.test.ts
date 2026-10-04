import { describe, expect, test } from "bun:test";
import {
  inferenceProviderDraft,
  inferenceRouteDraft,
} from "../../src/features/settings/inference-drafts";
import type { InferenceProviderSettings } from "../../src/features/settings/settings-api";

const revision = "2026-10-04T02:00:00Z";
const provider = {
  id: "provider",
  name: "Latest provider",
  kind: "openai" as const,
  protocol: "openai-compatible",
  endpoint: "https://example.test/v1/",
  hasApiKey: true,
  updatedAt: revision,
};
const settings: InferenceProviderSettings = {
  providers: [provider],
  presets: [
    {
      kind: "openai",
      name: "OpenAI",
      endpoint: "https://api.openai.com/v1/",
      endpointPlaceholder: "",
      supportsEmbedding: true,
    },
  ],
  routes: ["embedding", "chat"].map((capability, index) => ({
    capability: capability as "embedding" | "chat",
    providerId: index ? "another-provider" : provider.id,
    providerName: index ? "Another provider" : provider.name,
    protocol: provider.protocol,
    endpoint: provider.endpoint,
    model: index ? "other-chat" : "latest-embedding",
    dimensions: index ? null : 1024,
    timeoutSeconds: 60,
    hasApiKey: true,
    updatedAt: `2026-10-04T0${index + 3}:00:00Z`,
    runtimeState: "configured",
    indexState: "unknown",
    isApplied: false,
  })),
};

describe("Latest Inference drafts", () => {
  test("a route reload combines the latest route and provider revisions", () => {
    expect(inferenceRouteDraft(settings.routes[0]!, settings)).toEqual({
      providerId: provider.id,
      providerUpdatedAt: revision,
      model: "latest-embedding",
      dimensions: 1024,
      timeoutSeconds: 60,
      updatedAt: settings.routes[0]!.updatedAt,
    });
    expect(
      inferenceRouteDraft(
        { ...settings.routes[0]!, providerId: null, model: null },
        settings,
      ),
    ).toMatchObject({ providerId: null, providerUpdatedAt: null, model: "" });
  });

  test("a provider reload keeps current assignments and clears a replacement credential", () => {
    const draft = inferenceProviderDraft(provider, settings);
    expect(draft).toMatchObject({
      id: provider.id,
      name: provider.name,
      updatedAt: revision,
      apiKey: "",
      embeddingUpdatedAt: settings.routes[0]!.updatedAt,
      chatUpdatedAt: settings.routes[1]!.updatedAt,
      embedding: {
        enabled: true,
        model: "latest-embedding",
        dimensions: 1024,
        timeoutSeconds: 60,
      },
      chat: { enabled: false, model: "", dimensions: null, timeoutSeconds: 30 },
    });
    expect(settings.routes[1]!.providerId).toBe("another-provider");
  });
});
