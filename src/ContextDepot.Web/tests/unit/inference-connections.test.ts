import { describe, expect, test } from "bun:test";
import {
  availableInferenceProviders,
  connectedInferenceProviders,
  connectInferenceProviderDraft,
} from "../../src/features/settings/inference-connections";
import type {
  InferenceProviderDraft,
  InferenceProviderSettings,
} from "../../src/features/settings/settings-api";

const revision = "2026-10-03T00:00:00Z";
const settings: InferenceProviderSettings = {
  providers: [
    {
      id: "openai",
      name: "OpenAI",
      kind: "openai",
      endpoint: "https://api.openai.com/v1/",
      hasApiKey: false,
      protocol: "openai-compatible",
      updatedAt: revision,
    },
    {
      id: "deepseek",
      name: "DeepSeek",
      kind: "deepseek",
      endpoint: "https://api.deepseek.com/v1/",
      hasApiKey: true,
      protocol: "openai-compatible",
      updatedAt: revision,
    },
  ],
  presets: [
    {
      kind: "openai",
      name: "OpenAI",
      endpoint: "https://api.openai.com/v1/",
      endpointPlaceholder: "https://api.openai.com/v1/",
      supportsEmbedding: true,
    },
    {
      kind: "deepseek",
      name: "DeepSeek",
      endpoint: "https://api.deepseek.com/v1/",
      endpointPlaceholder: "https://api.deepseek.com/v1/",
      supportsEmbedding: false,
    },
  ],
  routes: [],
};
const draft: InferenceProviderDraft = {
  id: null,
  kind: "openai",
  name: "OpenAI",
  endpoint: "",
  apiKey: "previous-key",
  updatedAt: null,
  embeddingUpdatedAt: revision,
  chatUpdatedAt: revision,
  embedding: {
    enabled: false,
    model: "old-model",
    dimensions: 3,
    timeoutSeconds: 30,
  },
  chat: {
    enabled: false,
    model: "old-chat",
    dimensions: null,
    timeoutSeconds: 30,
  },
};

describe("Inference connection choices", () => {
  test("hides unused presets but preserves an assigned provider with missing credentials", () => {
    expect(
      connectedInferenceProviders(settings).map((provider) => provider.id),
    ).toEqual(["deepseek"]);
    expect(
      connectedInferenceProviders({
        ...settings,
        routes: [
          {
            providerId: "openai",
          } as InferenceProviderSettings["routes"][number],
        ],
      }).map((provider) => provider.id),
    ).toEqual(["openai", "deepseek"]);
    expect(availableInferenceProviders(settings, "embedding")).toEqual([]);
    expect(
      availableInferenceProviders(settings, "chat").map(
        (provider) => provider.id,
      ),
    ).toEqual(["deepseek"]);
  });

  test("connecting a preset reuses its existing revision and configures only the requested capability", () => {
    const next = connectInferenceProviderDraft(
      draft,
      settings.presets[0]!,
      settings,
      "embedding",
    );
    expect(next.id).toBe("openai");
    expect(next.updatedAt).toBe(revision);
    expect(next.apiKey).toBe("");
    expect(next.embedding).toEqual({
      enabled: true,
      model: "",
      dimensions: null,
      timeoutSeconds: 30,
    });
    expect(next.chat.enabled).toBe(false);
    expect(next.chatUpdatedAt).toBe(revision);
  });

  test("a connected or assigned provider is never repurposed by the new connection form", () => {
    const next = connectInferenceProviderDraft(
      draft,
      settings.presets[1]!,
      settings,
      "chat",
    );
    expect(next.id).toBeNull();
    expect(next.updatedAt).toBeNull();
    expect(next.chat.enabled).toBe(true);
    expect(next.embedding.enabled).toBe(false);
    const assigned = connectInferenceProviderDraft(
      draft,
      settings.presets[0]!,
      {
        ...settings,
        routes: [
          {
            providerId: "openai",
          } as InferenceProviderSettings["routes"][number],
        ],
      },
      "embedding",
    );
    expect(assigned.id).toBeNull();
  });
});
