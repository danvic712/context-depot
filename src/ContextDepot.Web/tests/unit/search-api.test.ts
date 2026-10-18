import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  parseSearch,
  parsePreview,
  searchKnowledge,
  getKnowledgePreview,
  getSearchWorkspaces,
  parseRecentKnowledge,
  getRecentKnowledge,
} from "../../src/features/knowledge/search-api";
import { knowledgeKey } from "../../src/features/knowledge/knowledge-models";

const response = {
  items: [
    {
      id: "same",
      type: "document",
      workspace: "projects",
      kind: "document",
      title: "Design",
      excerpt: "First matching chunk",
    },
    {
      id: "same",
      type: "context",
      workspace: "projects",
      kind: "decision",
      title: "choice",
      excerpt: "Current decision",
    },
    {
      id: "same",
      type: "document",
      workspace: "projects",
      kind: "document",
      title: "Design",
      excerpt: "Second matching chunk",
    },
  ],
  degraded: true,
  limit: 15,
};
const detail = {
  id: "same",
  type: "document",
  workspace: "projects",
  title: "Design",
  content: "# Full source",
  updatedAt: "2026-10-01T00:00:00Z",
};

describe("Knowledge search API", () => {
  test("recent knowledge uses real update dates and rejects malformed collections", () => {
    const recent = {
      asOf: detail.updatedAt,
      hasMore: false,
      items: [
        {
          ...detail,
          kind: null,
          workspace: { id: "space", path: "projects/notes" },
          indexStatus: "pending",
        },
      ],
    };
    const parsed = parseRecentKnowledge(recent);
    expect(parsed.items[0]?.updatedAt).toBe(detail.updatedAt);
    expect(parsed.items[0]?.workspace).toBe("projects/notes");
    expect(parsed.items[0]?.excerpt).toBe("");
    expect(parsed.degraded).toBe(false);
    expect(() => parseRecentKnowledge({ ...recent, asOf: "bad" })).toThrow();
    expect(() => parseRecentKnowledge({ ...recent, hasMore: null })).toThrow();
    expect(() =>
      parseRecentKnowledge({
        ...recent,
        items: new Array(21).fill(recent.items[0]),
      }),
    ).toThrow();
  });
  test("recent requests send filters before the server applies its limit", async () => {
    const paths: string[] = [];
    const server = Bun.serve({
      port: 0,
      fetch(request) {
        const url = new URL(request.url);
        paths.push(url.pathname + url.search);
        return Response.json({
          asOf: detail.updatedAt,
          items: [],
          hasMore: false,
        });
      },
    });
    const interceptor = httpClient.interceptors.request.use((config) => ({
      ...config,
      baseURL: `${server.url}api`,
    }));
    try {
      await getRecentKnowledge(
        "projects/notes",
        "contexts",
        "decision",
        new AbortController().signal,
      );
      expect(paths).toEqual([
        "/api/knowledge/recent?workspace=projects%2Fnotes&type=contexts&kind=decision&limit=20",
      ]);
    } finally {
      httpClient.interceptors.request.eject(interceptor);
      server.stop(true);
    }
  });
  test("deduplicates document chunks without conflating IDs across types", () => {
    const result = parseSearch(response);
    expect(result.items).toHaveLength(2);
    expect(result.items[1]!.title).toBe("choice");
    expect(result.items[0]!.excerpt).toBe("First matching chunk");
    expect(result.limit).toBe(15);
    expect(knowledgeKey(result.items[0]!)).not.toBe(
      knowledgeKey(result.items[1]!),
    );
    expect(result.degraded).toBe(true);
  });
  test("rejects malformed collections and preview timestamps instead of showing empty success", () => {
    expect(() => parseSearch({ ...response, items: null })).toThrow();
    expect(() => parseSearch({ ...response, degraded: undefined })).toThrow();
    expect(() => parseSearch({ ...response, limit: 0 })).toThrow();
    expect(() =>
      parseSearch({
        ...response,
        items: [{ ...response.items[0], type: "unknown" }],
      }),
    ).toThrow();
    expect(() => parsePreview({ ...detail, type: "unknown" })).toThrow();
    expect(() => parsePreview({ ...detail, updatedAt: "invalid" })).toThrow();
    expect(parsePreview(detail).content).toBe("# Full source");
  });
  test("passes query, exact space scope, cancellation and full-content resource paths", async () => {
    const paths: string[] = [];
    const server = Bun.serve({
      port: 0,
      fetch(request) {
        const url = new URL(request.url);
        paths.push(url.pathname + url.search);
        return Response.json(
          url.pathname.endsWith("/search")
            ? response
            : url.pathname.endsWith("/workspaces")
              ? [
                  {
                    id: "workspace",
                    name: "Project notes",
                    path: "projects/notes",
                  },
                ]
              : detail,
        );
      },
    });
    const original = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `http://localhost:${server.port}/api`;
    try {
      const result = await searchKnowledge(
        "decision & notes",
        "projects/notes",
        undefined,
        "decision",
      );
      await searchKnowledge("decision", undefined);
      const document = await getKnowledgePreview(result.items[0]!);
      const spaces = await getSearchWorkspaces();
      expect(document.content).toBe("# Full source");
      expect(spaces[0]!.path).toBe("projects/notes");
      expect(spaces[0]!.name).toBe("Project notes");
      const first = new URL(paths[0]!, "http://localhost");
      expect(first.searchParams.get("query")).toBe("decision & notes");
      expect(first.searchParams.get("workspace")).toBe("projects/notes");
      expect(first.searchParams.get("kind")).toBe("decision");
      expect(paths[1]).not.toContain("workspace");
      expect(paths[2]).toBe("/api/knowledge/document/same");
      await searchKnowledge("decision", "all");
      expect(
        new URL(paths[4]!, "http://localhost").searchParams.get("workspace"),
      ).toBe("all");
      const controller = new AbortController();
      controller.abort();
      await expect(
        searchKnowledge("canceled", undefined, controller.signal),
      ).rejects.toThrow();
      expect(paths).toHaveLength(5);
    } finally {
      httpClient.defaults.baseURL = original;
      await server.stop(true);
    }
  });
  test("rejects detail responses for a different ID or knowledge type", async () => {
    let returned = detail;
    const server = Bun.serve({
      port: 0,
      fetch: () => Response.json(returned),
    });
    const original = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `http://localhost:${server.port}/api`;
    try {
      returned = { ...detail, id: "another-record" };
      await expect(
        getKnowledgePreview({ id: "same", type: "document" }),
      ).rejects.toThrow();
      returned = { ...detail, type: "context" };
      await expect(
        getKnowledgePreview({ id: "same", type: "document" }),
      ).rejects.toThrow();
      returned = { ...detail, id: "01a0fc52-fe83-7458-add6-6106ad706243" };
      const result = await getKnowledgePreview({
        id: returned.id.toUpperCase(),
        type: "document",
      });
      expect(result.id).toBe(returned.id);
    } finally {
      httpClient.defaults.baseURL = original;
      await server.stop(true);
    }
  });
  test("space choices require a usable display name without changing the scope path", async () => {
    let name: unknown = "研究笔记";
    const server = Bun.serve({
      port: 0,
      fetch: () =>
        Response.json([{ id: "space", name, path: "projects/notes" }]),
    });
    const original = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `http://localhost:${server.port}/api`;
    try {
      expect(await getSearchWorkspaces()).toEqual([
        { id: "space", name: "研究笔记", path: "projects/notes" },
      ]);
      for (name of [undefined, null, "   ", 42])
        await expect(getSearchWorkspaces()).rejects.toThrow();
    } finally {
      httpClient.defaults.baseURL = original;
      await server.stop(true);
    }
  });
});
