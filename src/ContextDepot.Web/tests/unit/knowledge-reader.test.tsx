import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router";
import { KnowledgeReaderView } from "../../src/features/knowledge/KnowledgeReader";
import { KnowledgeContent } from "../../src/features/knowledge/KnowledgeContent";
import { knowledgeReturnTarget } from "../../src/features/knowledge/knowledge-navigation";
import type { KnowledgePreview } from "../../src/features/knowledge/search-api";
import type { Resource } from "../../src/hooks/use-request-resource";
import { initializeI18n } from "../../src/lib/i18n";

const detail: KnowledgePreview = {
  id: "knowledge",
  type: "document",
  title: "Reading source",
  workspace: "projects/reader",
  content:
    "# Canonical source\n\nFull content after the search excerpt.\n\n| Name | Value |\n| --- | --- |\n| Source | Canonical |\n\n```ts\nconst source = true;\n```",
  updatedAt: "2026-10-02T00:00:00Z",
};

async function language(lang: "en" | "zh" = "en") {
  await initializeI18n(lang, async (requested) => {
    const directory = new URL(
      `../../../../locales/${requested === "zh" ? "zh-CN" : "en-US"}/`,
      import.meta.url,
    );
    return Object.assign(
      {},
      ...(await Promise.all(
        ["knowledge-search", "ui-states"].map((name) =>
          Bun.file(new URL(`${name}.json`, directory)).json(),
        ),
      )),
    );
  });
}

function render(resource: Resource<KnowledgePreview>, source?: unknown) {
  return renderToStaticMarkup(
    <MemoryRouter>
      <KnowledgeReaderView
        resource={resource}
        source={source}
        onRetry={() => {}}
      />
    </MemoryRouter>,
  );
}

describe("Knowledge reader", () => {
  test("standalone pages share full Markdown with previews and expose a single page title", async () => {
    await language();
    for (const type of ["context", "document"] as const) {
      const record = { ...detail, type };
      const html = render(
        { data: record, pending: false },
        { from: "/", navigation: "home" },
      );
      expect(html).toContain('<h1 tabindex="-1">Reading source</h1>');
      expect(html).toContain("<h2>Canonical source</h2>");
      expect(html).toContain("<table>");
      expect(html).toContain("const source = true;");
      expect(html).toContain("Full content after the search excerpt.");
      expect(html).toContain('href="/"');
      expect(html).toContain('dateTime="2026-10-02T00:00:00Z"');
      expect(html).not.toContain(' disabled=""');
      const preview = renderToStaticMarkup(
        <KnowledgeContent detail={record} />,
      );
      expect(preview).toContain("Full content after the search excerpt.");
      expect(preview).toContain("<table>");
    }
  });

  test("loading announces progress and prevents copying unavailable content", async () => {
    await language();
    const html = render({ pending: true });
    expect(html).toContain('aria-busy="true"');
    expect(html).toContain('role="status" aria-label="Loading"');
    expect(html).toContain('data-slot="skeleton"');
    expect(html).toContain(' disabled=""');
    expect(html).not.toContain("Reading source");
  });

  test("errors hide previous content and copy; only recoverable failures offer retry", async () => {
    for (const lang of ["en", "zh"] as const) {
      await language(lang);
      for (const kind of ["notFound", "forbidden", "unavailable"] as const) {
        const retryable = kind === "unavailable";
        const html = render({
          data: detail,
          pending: false,
          error: { kind, retryable },
        });
        expect(html).toContain('role="alert"');
        expect(html).toContain(' disabled=""');
        expect(html).not.toContain("Reading source");
        expect(html).not.toContain("Full content after the search excerpt.");
        expect(html.includes(lang === "zh" ? ">重试<" : ">Retry<")).toBe(
          retryable,
        );
        expect(html).not.toContain("knowledgeLoadError");
      }
    }
  });
});

describe("Knowledge source navigation", () => {
  test("keeps valid source queries and selection without relying on browser history", () => {
    for (const from of [
      "/",
      "/?welcome=1",
      "/search?q=reader&type=documents&selected=document%3Aknowledge&read=1",
      "/spaces",
      "/spaces/projects%2Freader?q=notes#content",
    ]) {
      expect(knowledgeReturnTarget({ from }).to).toBe(from);
    }
    expect(knowledgeReturnTarget({ from: "/" }).label).toBe(
      "knowledgeBackHome",
    );
    expect(knowledgeReturnTarget({ from: "/spaces/projects" }).label).toBe(
      "knowledgeBackSpaces",
    );
  });
  test("direct links and untrusted source locations return safely to Search", () => {
    for (const state of [
      undefined,
      null,
      {},
      { from: "https://example.com" },
      { from: "//example.com" },
      { from: "/\\example.com" },
      { from: "/search\n" },
      { from: "/settings" },
      { from: "/documents/other" },
      { from: 1 },
    ]) {
      expect(knowledgeReturnTarget(state)).toEqual({
        to: "/search",
        label: "knowledgeBackSearch",
      });
    }
  });
});
