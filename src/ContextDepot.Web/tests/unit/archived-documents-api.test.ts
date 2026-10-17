import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import { deleteKnowledge } from "../../src/features/knowledge/knowledge-management-api";
import {
  parseArchivedDocuments,
  getArchivedDocuments,
} from "../../src/features/knowledge/document-archive-api";

const document = {
  id: "archived-id",
  title: "Old notes",
  path: "docs/old.md",
  updatedAt: "2026-10-15T00:00:00.123456Z",
};
const page = { items: [document], totalCount: 1, page: 1, pageSize: 12 };
describe("Archived document contracts", () => {
  test("rejects malformed or duplicate records and unbounded pagination", () => {
    expect(parseArchivedDocuments(page).items[0]?.updatedAt).toBe(
      document.updatedAt,
    );
    for (const invalid of [
      { ...page, page: 100001 },
      { ...page, pageSize: 61 },
      { ...page, totalCount: 0 },
      { ...page, items: [{ ...document, updatedAt: "invalid" }] },
      { ...page, items: [{ ...document, id: "" }] },
      { ...page, items: [{ ...document, path: "" }] },
      { ...page, items: [document, document], totalCount: 2 },
    ])
      expect(() => parseArchivedDocuments(invalid)).toThrow();
  });
  test("lists only the chosen space and deletes with management protection and the precise observed revision", async () => {
    const requests: {
      url: string;
      method: string;
      management: string | null;
    }[] = [];
    const server = Bun.serve({
      port: 0,
      fetch(request) {
        requests.push({
          url: request.url,
          method: request.method,
          management: request.headers.get("X-ContextDepot-Management"),
        });
        return request.method === "DELETE"
          ? new Response(null, { status: 204 })
          : Response.json(page);
      },
    });
    const interceptor = httpClient.interceptors.request.use((config) => ({
      ...config,
      baseURL: `${server.url}api`,
    }));
    const controller = new AbortController();
    try {
      expect(
        (await getArchivedDocuments("space/notes", 1, 12, controller.signal))
          .items[0]?.id,
      ).toBe(document.id);
      await deleteKnowledge(
        { ...document, type: "document" },
        controller.signal,
      );
      expect(new URL(requests[0]!.url).pathname).toBe(
        "/api/workspaces/space%2Fnotes/archived-documents",
      );
      expect(requests[1]!.method).toBe("DELETE");
      expect(requests[1]!.management).toBe("web");
      expect(
        new URL(requests[1]!.url).searchParams.get("expectedUpdatedAt"),
      ).toBe(document.updatedAt);
      await deleteKnowledge(
        { ...document, type: "context" },
        controller.signal,
      );
      expect(new URL(requests[2]!.url).pathname).toBe(
        "/api/contexts/archived-id",
      );
      expect(requests[2]!.management).toBe("web");
      expect(
        new URL(requests[2]!.url).searchParams.get("expectedUpdatedAt"),
      ).toBe(document.updatedAt);
      controller.abort();
      await expect(
        deleteKnowledge({ ...document, type: "document" }, controller.signal),
      ).rejects.toThrow();
      expect(requests).toHaveLength(3);
    } finally {
      httpClient.interceptors.request.eject(interceptor);
      server.stop(true);
    }
  });
});
