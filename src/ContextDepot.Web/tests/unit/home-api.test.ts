import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  createWorkspace,
  getKnowledge,
  getReadiness,
  getWorkspaces,
  parseKnowledge,
  parseWorkspace,
} from "../../src/features/home/home-api";

const now = "2026-10-01T00:00:00Z";
const workspace = {
  id: "workspace-id",
  name: "Research",
  description: null,
  path: "research",
  contextCount: 1,
  documentCount: 2,
  activityAt: now,
};
const context = {
  id: "shared-id",
  type: "context",
  kind: "decision",
  title: "A decision",
  workspace: { id: workspace.id, path: workspace.path },
  updatedAt: now,
  indexStatus: null,
};
const document = {
  ...context,
  type: "document",
  kind: null,
  indexStatus: "failed",
};

describe("Home resource contracts", () => {
  test("rejects malformed counts, timestamps and discriminated knowledge records", () => {
    expect(parseWorkspace(workspace).contextCount).toBe(1);
    expect(() => parseWorkspace({ ...workspace, contextCount: -1 })).toThrow();
    expect(() =>
      parseWorkspace({ ...workspace, activityAt: "invalid" }),
    ).toThrow();
    expect(parseKnowledge(context).kind).toBe("decision");
    expect(parseKnowledge(document).indexStatus).toBe("failed");
    expect(() => parseKnowledge({ ...context, kind: "unknown" })).toThrow();
    expect(() => parseKnowledge({ ...document, kind: "decision" })).toThrow();
  });

  test("uses resource paths, preserves same-ID records across types and parses 503 readiness", async () => {
    const paths: string[] = [];
    const bodies: unknown[] = [];
    const server = Bun.serve({
      port: 0,
      async fetch(request) {
        const url = new URL(request.url);
        paths.push(url.pathname + url.search);
        if (url.pathname === "/readyz")
          return Response.json(
            {
              status: "unhealthy",
              entries: { ignored: { description: "sensitive diagnostics" } },
            },
            { status: 503 },
          );
        if (request.method === "POST") {
          bodies.push(await request.json());
          return Response.json(workspace, { status: 201 });
        }
        return Response.json({
          asOf: now,
          items:
            url.pathname === "/api/workspaces"
              ? [workspace]
              : [context, document],
          hasMore: true,
        });
      },
    });
    const original = httpClient.defaults.baseURL;
    const interceptor = httpClient.interceptors.request.use((config) => {
      config.baseURL =
        config.url === "/readyz" ? server.url.toString() : `${server.url}api`;
      return config;
    });
    try {
      expect((await getWorkspaces()).hasMore).toBe(true);
      expect((await getKnowledge()).items.map((item) => item.type)).toEqual([
        "context",
        "document",
      ]);
      expect(
        (
          await createWorkspace({
            name: "Research",
            path: "research",
            description: "",
          })
        ).name,
      ).toBe("Research");
      expect(await getReadiness()).toBe("unhealthy");
      expect(paths).toEqual([
        "/api/workspaces?sort=-activityAt&limit=3",
        "/api/knowledge?sort=-updatedAt&limit=3",
        "/api/workspaces",
        "/readyz",
      ]);
      expect(bodies).toEqual([
        { name: "Research", path: "research", description: "" },
      ]);
    } finally {
      httpClient.interceptors.request.eject(interceptor);
      httpClient.defaults.baseURL = original;
      server.stop(true);
    }
  });
});
