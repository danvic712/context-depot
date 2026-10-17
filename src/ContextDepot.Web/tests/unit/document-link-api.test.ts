import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import { resolveDocumentLink } from "../../src/features/knowledge/document-link-api";

describe("Document link resolution", () => {
  test("preserves the source, encoded Markdown path, scope and cancellation", async () => {
    const requests: URL[] = [];
    const targetId = "22222222-2222-2222-2222-222222222222";
    const server = Bun.serve({
      port: 0,
      fetch(request) {
        requests.push(new URL(request.url));
        return Response.json({ id: targetId });
      },
    });
    const original = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `http://localhost:${server.port}/api`;
    try {
      const path = "../中文%20指南.md";
      expect(await resolveDocumentLink("source/id", path, "space/id")).toBe(
        targetId,
      );
      expect(requests[0]!.pathname).toBe(
        "/api/knowledge/document/source%2Fid/link",
      );
      expect(requests[0]!.searchParams.get("path")).toBe(path);
      expect(requests[0]!.searchParams.get("workspaceId")).toBe("space/id");
      await resolveDocumentLink("source", "index.md");
      expect(requests[1]!.searchParams.has("workspaceId")).toBe(false);
      const controller = new AbortController();
      controller.abort();
      await expect(
        resolveDocumentLink("source", "index.md", undefined, controller.signal),
      ).rejects.toThrow();
      expect(requests).toHaveLength(2);
    } finally {
      httpClient.defaults.baseURL = original;
      await server.stop(true);
    }
  });

  test("rejects missing and malformed identities before a reader can request them", async () => {
    let response: unknown;
    const server = Bun.serve({ port: 0, fetch: () => Response.json(response) });
    const original = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `http://localhost:${server.port}/api`;
    try {
      for (response of [
        {},
        { id: "../other" },
        { id: "https://example.com" },
        { id: 123 },
        null,
      ]) {
        await expect(
          resolveDocumentLink("source", "target.md"),
        ).rejects.toThrow();
      }
    } finally {
      httpClient.defaults.baseURL = original;
      await server.stop(true);
    }
  });
});
