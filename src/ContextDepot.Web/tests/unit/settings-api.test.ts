import { describe, expect, test } from "bun:test";
import {
  parseInferenceRoute,
  parseKeys,
  parseOverview,
  parseIssuedKey,
  validateInferenceDraft,
  type InferenceDraft,
  parseInferenceProviders,
  validateInferenceProviderDraft,
  type InferenceProviderDraft,
  applyInferenceProviderPreset,
} from "../../src/features/settings/settings-api";
const draft: InferenceDraft = {
  providerName: "Provider",
  endpoint: "https://example.test/v1",
  model: "model",
  dimensions: 3,
  timeoutSeconds: 30,
  apiKey: "example-key",
  updatedAt: "2026-10-02T00:00:00Z",
};
describe("Settings contracts", () => {
  test("shared provider validates each selected model and permits keeping one stored key", () => {
    const provider: InferenceProviderDraft = {
      id: "provider",
      name: "Provider",
      kind: "custom",
      endpoint: draft.endpoint,
      apiKey: "",
      updatedAt: draft.updatedAt,
      embeddingUpdatedAt: draft.updatedAt,
      chatUpdatedAt: draft.updatedAt,
      embedding: {
        enabled: true,
        model: "embed-model",
        dimensions: 3,
        timeoutSeconds: 30,
      },
      chat: {
        enabled: true,
        model: "chat-model",
        dimensions: null,
        timeoutSeconds: 60,
      },
    };
    expect(validateInferenceProviderDraft(provider, true)).toEqual([]);
    expect(validateInferenceProviderDraft(provider, false)).toContain("apiKey");
    expect(
      validateInferenceProviderDraft(
        {
          ...provider,
          embedding: { ...provider.embedding, dimensions: 0 },
          chat: { ...provider.chat, model: "" },
        },
        true,
      ),
    ).toEqual(["embedding.dimensions", "chat.model"]);
    expect(() =>
      parseInferenceProviders({ providers: [], routes: [] }),
    ).toThrow();
  });
  test("presets clear previous credentials and models without using the Azure placeholder as an endpoint", () => {
    const provider: InferenceProviderDraft = {
      id: null,
      name: "Provider",
      kind: "custom",
      endpoint: draft.endpoint,
      apiKey: "example-key",
      updatedAt: null,
      embeddingUpdatedAt: draft.updatedAt,
      chatUpdatedAt: draft.updatedAt,
      embedding: {
        enabled: true,
        model: "embedding",
        dimensions: 3,
        timeoutSeconds: 30,
      },
      chat: {
        enabled: true,
        model: "chat",
        dimensions: null,
        timeoutSeconds: 30,
      },
    };
    const azure = applyInferenceProviderPreset(provider, {
      kind: "azure-openai",
      name: "Azure OpenAI",
      endpoint: null,
      endpointPlaceholder: "https://YOUR-RESOURCE.openai.azure.com/openai/v1/",
      supportsEmbedding: true,
    });
    expect(azure.endpoint).toBe("");
    expect(azure.apiKey).toBe("");
    expect(azure.embedding.enabled).toBe(false);
    expect(azure.chat.model).toBe("");
    expect(
      validateInferenceProviderDraft({ ...provider, kind: "deepseek" }, true),
    ).toContain("embedding.unsupported");
    expect(
      validateInferenceProviderDraft(
        { ...provider, kind: "azure-openai" },
        true,
      ),
    ).toContain("endpoint");
    expect(
      validateInferenceProviderDraft(
        {
          ...provider,
          kind: "azure-openai",
          endpoint: "https://resource.openai.azure.com/openai/v1/",
        },
        true,
      ),
    ).toEqual([]);
    expect(
      validateInferenceProviderDraft(
        {
          ...provider,
          embedding: { ...provider.embedding, enabled: false },
          chat: { ...provider.chat, enabled: false },
        },
        false,
      ),
    ).toEqual([]);
  });
  test("Inference configuration validates both capabilities and key replacement semantics", () => {
    expect(validateInferenceDraft(draft, "embedding", false)).toEqual([]);
    expect(
      validateInferenceDraft({ ...draft, dimensions: null }, "chat", false),
    ).toEqual([]);
    expect(
      validateInferenceDraft({ ...draft, apiKey: "" }, "embedding", true),
    ).toEqual([]);
    expect(
      validateInferenceDraft({ ...draft, apiKey: "" }, "embedding", false),
    ).toContain("apiKey");
  });
  test.each([
    "ftp://example.test",
    "/v1",
    "https://user:secret@example.test",
    "https://example.test?secret=x",
    "https://example.test/#x",
  ])("rejects unsafe endpoint %s", (endpoint) => {
    expect(
      validateInferenceDraft({ ...draft, endpoint }, "embedding", true),
    ).toContain("endpoint");
  });
  test("invalid integer dimensions and timeouts are rejected", () => {
    expect(
      validateInferenceDraft(
        { ...draft, dimensions: 1.5, timeoutSeconds: 301 },
        "embedding",
        true,
      ),
    ).toEqual(["dimensions", "timeoutSeconds"]);
  });
  test("read contracts never carry persisted or plaintext secrets into page state", () => {
    const key = {
      id: "id",
      name: "client",
      prefix: "cdk_test",
      createdAt: draft.updatedAt,
      lastUsedAt: null,
      revokedAt: null,
      workspaceIds: ["workspace"],
      secretHash: "example-hash",
      secret: "example-secret",
    };
    expect(
      parseKeys({
        items: [key],
        workspaces: [{ id: "workspace", name: "Research", path: "research" }],
      }).items[0],
    ).not.toHaveProperty("secretHash");
    expect(
      parseKeys({ items: [key], workspaces: [] }).items[0],
    ).not.toHaveProperty("secret");
    const route = {
      capability: "embedding",
      providerName: "Provider",
      protocol: "openai-compatible",
      endpoint: draft.endpoint,
      model: "model",
      dimensions: 3,
      timeoutSeconds: 30,
      hasApiKey: true,
      updatedAt: draft.updatedAt,
      runtimeState: "active",
      indexState: "pending",
      isApplied: true,
      apiKey: "example-key",
    };
    expect(parseInferenceRoute(route)).not.toHaveProperty("apiKey");
    expect(() =>
      parseInferenceRoute({ ...route, hasApiKey: "true" }),
    ).toThrow();
  });
  test("only a valid one-time issued secret can enter the issuance dialog", () => {
    const key = {
      id: "id",
      name: "client",
      prefix: "cdk_test",
      createdAt: draft.updatedAt,
      lastUsedAt: null,
      revokedAt: null,
      workspaceIds: ["workspace"],
    };
    expect(() =>
      parseIssuedKey({ key, secret: "wrong-prefix." + "x".repeat(32) }),
    ).toThrow();
    expect(
      parseIssuedKey({ key, secret: "cdk_test." + "x".repeat(32) }).key.name,
    ).toBe("client");
  });
  test("MCP paths and status values must come from the supported Host contract", () => {
    const overview = {
      depotName: "My Depot",
      databaseState: "available",
      markdownState: "available",
      semanticState: "unconfigured",
      indexState: "unconfigured",
      indexedCount: null,
      totalCount: null,
      mcpPath: "/mcp",
    };
    expect(parseOverview(overview).semanticState).toBe("unconfigured");
    expect(() =>
      parseOverview({ ...overview, mcpPath: "https://external.example/mcp" }),
    ).toThrow();
    expect(() =>
      parseOverview({ ...overview, databaseState: "healthy" }),
    ).toThrow();
  });
});
