import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  saveInferenceRoute,
  validateInferenceRouteDraft,
  type InferenceProviderSettings,
  type InferenceRouteDraft,
} from "../../src/features/settings/settings-api";

const revision = "2026-10-03T00:00:00Z";
const draft: InferenceRouteDraft = {
  providerId: "deepseek-provider",
  providerUpdatedAt: revision,
  model: " deepseek-chat ",
  dimensions: null,
  timeoutSeconds: 30,
  updatedAt: revision,
};
const settings: InferenceProviderSettings = {
  providers: [
    {
      id: "deepseek-provider",
      name: "DeepSeek",
      kind: "deepseek",
      endpoint: "https://api.deepseek.com/v1/",
      protocol: "openai-compatible",
      hasApiKey: true,
      updatedAt: revision,
    },
    {
      id: "openai-provider",
      name: "OpenAI",
      kind: "openai",
      endpoint: "https://api.openai.com/v1/",
      protocol: "openai-compatible",
      hasApiKey: false,
      updatedAt: revision,
    },
  ],
  routes: [],
  presets: [
    {
      kind: "deepseek",
      name: "DeepSeek",
      endpoint: "https://api.deepseek.com/v1/",
      endpointPlaceholder: "https://api.deepseek.com/v1/",
      supportsEmbedding: false,
    },
    {
      kind: "openai",
      name: "OpenAI",
      endpoint: "https://api.openai.com/v1/",
      endpointPlaceholder: "https://api.openai.com/v1/",
      supportsEmbedding: true,
    },
  ],
};

describe("Independent Inference routes", () => {
  test("requires a configured provider that supports the selected capability", () => {
    expect(validateInferenceRouteDraft(draft, "chat", settings)).toEqual([]);
    expect(
      validateInferenceRouteDraft(
        { ...draft, dimensions: 3 },
        "embedding",
        settings,
      ),
    ).toEqual(["providerId"]);
    expect(
      validateInferenceRouteDraft(
        { ...draft, providerId: "openai-provider" },
        "chat",
        settings,
      ),
    ).toEqual(["providerId"]);
    expect(
      validateInferenceRouteDraft(
        { ...draft, providerId: "missing-provider", providerUpdatedAt: null },
        "chat",
        settings,
      ),
    ).toEqual(["providerId"]);
    expect(
      validateInferenceRouteDraft(
        { ...draft, model: "", dimensions: 3, timeoutSeconds: 301 },
        "chat",
        settings,
      ),
    ).toEqual(["model", "dimensions", "timeoutSeconds"]);
    expect(
      validateInferenceRouteDraft(
        { ...draft, providerId: null, providerUpdatedAt: null },
        "chat",
        settings,
      ),
    ).toEqual([]);
  });

  test("route writes reference an existing provider and carry no connection credentials or other capability", async () => {
    const requests: {
      path: string;
      body: unknown;
      management: string | null;
    }[] = [];
    const server = Bun.serve({
      port: 0,
      async fetch(request) {
        const body = await request.json();
        requests.push({
          path: new URL(request.url).pathname,
          body,
          management: request.headers.get("X-ContextDepot-Management"),
        });
        return Response.json({
          ...body,
          capability: "chat",
          providerName: body.providerId ? "DeepSeek" : null,
          protocol: "openai-compatible",
          endpoint: body.providerId ? "https://api.deepseek.com/v1/" : null,
          hasApiKey: Boolean(body.providerId),
          runtimeState: body.providerId ? "configured" : "unconfigured",
          indexState: "not-applicable",
          isApplied: false,
        });
      },
    });
    const interceptor = httpClient.interceptors.request.use((config) => {
      config.baseURL = `${server.url}api`;
      return config;
    });
    try {
      const signal = new AbortController().signal;
      expect((await saveInferenceRoute("chat", draft, signal)).providerId).toBe(
        "deepseek-provider",
      );
      await saveInferenceRoute(
        "chat",
        { ...draft, providerId: null, providerUpdatedAt: null, dimensions: 3 },
        signal,
      );
      expect(requests).toEqual([
        {
          path: "/api/settings/inference/chat",
          management: "web",
          body: { ...draft, model: "deepseek-chat" },
        },
        {
          path: "/api/settings/inference/chat",
          management: "web",
          body: {
            ...draft,
            providerId: null,
            providerUpdatedAt: null,
            model: null,
            dimensions: null,
          },
        },
      ]);
    } finally {
      httpClient.interceptors.request.eject(interceptor);
      server.stop(true);
    }
  });
});
