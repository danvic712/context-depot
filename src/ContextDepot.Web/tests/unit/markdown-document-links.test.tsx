import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter, Routes, Route } from "react-router";
import { markdownDocumentLink } from "../../src/lib/markdown-url";
import { KnowledgeContent } from "../../src/features/knowledge/KnowledgeContent";

describe("Markdown document links", () => {
  test("recognizes local Markdown links and keeps external URLs and application routes", () => {
    for (const path of [
      "guide.md",
      "../guide.MD",
      "/docs/guide.md",
      "%E4%B8%AD%E6%96%87.md",
      "a%20b%2Emd",
    ])
      expect(markdownDocumentLink(`${path}#details`)).toEqual({
        path,
        hash: "#details",
      });
    expect(markdownDocumentLink("guide.md?raw=1#details")).toEqual({
      path: "guide.md",
      hash: "#details",
    });
    for (const href of [
      "https://example.com/guide.md",
      "//example.com/a.md",
      "#details",
      "/spaces",
      "guide.txt",
      "%invalid.md",
    ])
      expect(markdownDocumentLink(href)).toBeUndefined();
  });

  test("uses the source document identity in both full-page and workspace readers", () => {
    const detail = {
      id: "source-id",
      type: "document" as const,
      title: "Source",
      workspace: "project",
      content:
        "[Guide](../guide.md#details) [Web](https://example.com/guide.md) [Heading](#details)\n\n## Details",
      updatedAt: "2026-10-16T00:00:00Z",
    };
    for (const base of ["/knowledge", "/spaces/workspace-id"]) {
      const route =
        base === "/knowledge"
          ? "/knowledge/documents/:knowledgeId"
          : "/spaces/:spaceId/documents/:knowledgeId";
      const html = renderToStaticMarkup(
        <MemoryRouter initialEntries={[`${base}/documents/source-id`]}>
          <Routes>
            <Route
              path={route}
              element={<KnowledgeContent detail={detail} />}
            />
          </Routes>
        </MemoryRouter>,
      );
      expect(html).toContain(
        `href="${base}/documents/source-id/link?path=..%2Fguide.md#details"`,
      );
      expect(html).toContain('href="https://example.com/guide.md"');
      expect(html).toMatch(/href="#markdown-[^"]+-details"/);
    }
  });
});
