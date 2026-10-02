import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  parseSearch,
  parsePreview,
  searchKnowledge,
  getKnowledgePreview,
  getSearchWorkspaces,
  hitKey,
} from "../../src/features/knowledge/search-api";

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
  test("deduplicates document chunks without conflating IDs across types", () => {
    const result = parseSearch(response);
    expect(result.items).toHaveLength(2);
    expect(result.items[1]!.title).toBe("choice");
    expect(result.items[0]!.excerpt).toBe("First matching chunk");
    expect(result.limit).toBe(15);
    expect(hitKey(result.items[0]!)).not.toBe(hitKey(result.items[1]!));
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
              ? [{ id: "workspace", path: "projects/notes" }]
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
});
