import { describe, expect, test } from "bun:test";
import {
  parseAiRoute,
  parseKeys,
  parseOverview,
  parseIssuedKey,
  validateAiDraft,
  type AiDraft,
  parseAiProviders,
  validateAiProviderDraft,
  type AiProviderDraft,
} from "../../src/features/settings/settings-api";
const draft: AiDraft = {
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
    const provider: AiProviderDraft = {
      id: "provider",
      name: "Provider",
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
    expect(validateAiProviderDraft(provider, true)).toEqual([]);
    expect(validateAiProviderDraft(provider, false)).toContain("apiKey");
    expect(
      validateAiProviderDraft(
        {
          ...provider,
          embedding: { ...provider.embedding, dimensions: 0 },
          chat: { ...provider.chat, model: "" },
        },
        true,
      ),
    ).toEqual(["embedding.dimensions", "chat.model"]);
    expect(() => parseAiProviders({ providers: [], routes: [] })).toThrow();
  });
  test("AI configuration validates both capabilities and key replacement semantics", () => {
    expect(validateAiDraft(draft, "embedding", false)).toEqual([]);
    expect(
      validateAiDraft({ ...draft, dimensions: null }, "chat", false),
    ).toEqual([]);
    expect(
      validateAiDraft({ ...draft, apiKey: "" }, "embedding", true),
    ).toEqual([]);
    expect(
      validateAiDraft({ ...draft, apiKey: "" }, "embedding", false),
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
      validateAiDraft({ ...draft, endpoint }, "embedding", true),
    ).toContain("endpoint");
  });
  test("invalid integer dimensions and timeouts are rejected", () => {
    expect(
      validateAiDraft(
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
    expect(parseAiRoute(route)).not.toHaveProperty("apiKey");
    expect(() => parseAiRoute({ ...route, hasApiKey: "true" })).toThrow();
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
      version: null,
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
