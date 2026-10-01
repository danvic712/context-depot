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
  contexts: [
    {
      contextId: "same",
      workspace: "projects",
      kind: "decision",
      key: "choice",
      title: null,
      content: "Current decision",
    },
  ],
  documents: [
    {
      documentId: "same",
      workspace: "projects",
      title: "Design",
      path: "design.md",
      excerpt: "First matching chunk",
    },
    {
      documentId: "same",
      workspace: "projects",
      title: "Design",
      path: "design.md",
      excerpt: "Second matching chunk",
    },
  ],
  retrieval: { retrievalDegraded: true },
};
const detail = {
  id: "same",
  type: "document",
  workspace: "projects",
  title: "Design",
  content: "# Full source",
  updatedAt: "2026-10-01T00:00:00Z",
};

describe("Knowledge dialog API", () => {
  test("deduplicates document chunks without conflating IDs across types", () => {
    const result = parseSearch(response);
    expect(result.items).toHaveLength(2);
    expect(result.items[0]!.title).toBe("choice");
    expect(result.items[1]!.excerpt).toBe("First matching chunk");
    expect(hitKey(result.items[0]!)).not.toBe(hitKey(result.items[1]!));
    expect(result.degraded).toBe(true);
  });
  test("rejects malformed collections and preview timestamps instead of showing empty success", () => {
    expect(() => parseSearch({ ...response, documents: null })).toThrow();
    expect(() => parseSearch({ ...response, retrieval: {} })).toThrow();
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
      );
      await searchKnowledge("decision", undefined);
      const document = await getKnowledgePreview(result.items[1]!);
      const spaces = await getSearchWorkspaces();
      expect(document.content).toBe("# Full source");
      expect(spaces[0]!.path).toBe("projects/notes");
      const first = new URL(paths[0]!, "http://localhost");
      expect(first.searchParams.get("query")).toBe("decision & notes");
      expect(first.searchParams.get("workspace")).toBe("projects/notes");
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
});
