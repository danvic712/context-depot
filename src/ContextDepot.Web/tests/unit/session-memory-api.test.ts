import { afterAll, describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  getAgentSessionSettings,
  parseAgentSessionSettings,
  saveAgentSessionSettings,
} from "../../src/features/settings/settings-api";

let enabled = true;
let invalid = false;
let fail = false;
const requests: {
  method: string;
  path: string;
  management: string | null;
  body: unknown;
}[] = [];
const server = Bun.serve({
  port: 0,
  async fetch(request) {
    requests.push({
      method: request.method,
      path: new URL(request.url).pathname,
      management: request.headers.get("X-ContextDepot-Management"),
      body: request.method === "PUT" ? await request.json() : null,
    });
    if (fail) return new Response(null, { status: 503 });
    if (invalid) return Response.json({ enabled: "false" });
    if (request.method === "PUT")
      enabled = (requests.at(-1)!.body as { enabled: boolean }).enabled;
    return Response.json({ enabled });
  },
});
const baseURL = httpClient.defaults.baseURL;
httpClient.defaults.baseURL = `${server.url}api`;
afterAll(() => {
  server.stop(true);
  httpClient.defaults.baseURL = baseURL;
});

describe("Session memory API", () => {
  test("accepts only explicit boolean server values", () => {
    expect(parseAgentSessionSettings({ enabled: true })).toEqual({
      enabled: true,
    });
    expect(parseAgentSessionSettings({ enabled: false })).toEqual({
      enabled: false,
    });
    for (const value of [
      null,
      [],
      true,
      {},
      { enabled: null },
      { enabled: 0 },
      { enabled: "false" },
    ])
      expect(() => parseAgentSessionSettings(value)).toThrow();
  });

  test("loads server state and sends explicit management PUT toggles", async () => {
    const signal = new AbortController().signal;
    expect(await getAgentSessionSettings(signal)).toEqual({ enabled: true });
    expect(await saveAgentSessionSettings(false, signal)).toEqual({
      enabled: false,
    });
    expect(await getAgentSessionSettings(signal)).toEqual({ enabled: false });
    expect(await saveAgentSessionSettings(true, signal)).toEqual({
      enabled: true,
    });
    expect(requests).toEqual([
      {
        method: "GET",
        path: "/api/settings/agent-sessions",
        management: null,
        body: null,
      },
      {
        method: "PUT",
        path: "/api/settings/agent-sessions",
        management: "web",
        body: { enabled: false },
      },
      {
        method: "GET",
        path: "/api/settings/agent-sessions",
        management: null,
        body: null,
      },
      {
        method: "PUT",
        path: "/api/settings/agent-sessions",
        management: "web",
        body: { enabled: true },
      },
    ]);
  });

  test("rejects malformed responses and transport failures rather than inventing a value", async () => {
    const signal = new AbortController().signal;
    invalid = true;
    try {
      await expect(getAgentSessionSettings(signal)).rejects.toThrow();
      await expect(saveAgentSessionSettings(false, signal)).rejects.toThrow();
    } finally {
      invalid = false;
    }
    fail = true;
    try {
      await expect(saveAgentSessionSettings(false, signal)).rejects.toThrow();
    } finally {
      fail = false;
    }
    const controller = new AbortController();
    controller.abort();
    await expect(getAgentSessionSettings(controller.signal)).rejects.toThrow();
  });
});
