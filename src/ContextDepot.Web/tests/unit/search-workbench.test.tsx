import { beforeAll, describe, expect, test } from "bun:test";
import { createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router";
import { SearchFilters } from "../../src/features/knowledge/SearchFilters";
import { SearchResults } from "../../src/features/knowledge/SearchResults";
import { SearchPreview } from "../../src/features/knowledge/SearchPreview";
import { initializeI18n } from "../../src/lib/i18n";
import type { SearchHit } from "../../src/features/knowledge/search-api";
import {
  activeSearchFilters,
  filterSearchHits,
  matchingSearchSpaces,
} from "../../src/features/knowledge/search-filter-state";

beforeAll(async () => {
  await initializeI18n("en", async () =>
    Object.assign(
      {},
      ...(await Promise.all(
        [
          "knowledge-search",
          "ui-states",
          "workspace-browser",
          "navigation-and-actions",
        ].map((name) =>
          Bun.file(
            new URL(`../../../../locales/en-US/${name}.json`, import.meta.url),
          ).json(),
        ),
      )),
    ),
  );
});
const hits: SearchHit[] = [
  {
    id: "one",
    type: "document",
    title: "Deployment guide",
    workspace: "ops/linux",
    excerpt: "Search excerpt",
    kind: "",
  },
  {
    id: "two",
    type: "context",
    title: "Backup decision",
    workspace: "ops/linux",
    excerpt: "Preserve backups",
    kind: "decision",
  },
];
function filters(
  query = "",
  extra: Partial<Parameters<typeof SearchFilters>[0]> = {},
) {
  return renderToStaticMarkup(
    <MemoryRouter>
      <SearchFilters
        params={new URLSearchParams(query)}
        spaces={[
          { id: "linux", name: "Linux 运维", path: "ops/linux" },
          { id: "notes", name: "个人笔记", path: "personal" },
        ]}
        pending={false}
        onRetry={() => {}}
        onFilter={() => {}}
        onReset={() => {}}
        {...extra}
      />
    </MemoryRouter>,
  );
}

describe("Search workbench", () => {
  test("explicit all choices retain both resource types without reporting an active filter", () => {
    expect(filterSearchHits(hits, "all")).toEqual(hits);
    expect(filterSearchHits(hits, "contexts")).toEqual([hits[1]!]);
    expect(filterSearchHits(hits, "documents")).toEqual([hits[0]!]);
    expect(filterSearchHits(hits, "unsupported")).toEqual([]);
    expect(activeSearchFilters(new URLSearchParams("type=all&kind=all"))).toBe(
      0,
    );
    expect(
      activeSearchFilters(
        new URLSearchParams("type=contexts&kind=decision&workspace=all"),
      ),
    ).toBe(3);
  });
  test("a retained selected space cannot conceal an unsuccessful name search", () => {
    const spaces = [{ id: "one", name: "Linux 运维", path: "ops/linux" }];
    expect(matchingSearchSpaces(spaces, "  LINUX ")).toEqual(spaces);
    expect(matchingSearchSpaces(spaces, "运维")).toEqual(spaces);
    expect(matchingSearchSpaces(spaces, "ops/linux")).toEqual([]);
    expect(matchingSearchSpaces(spaces, "not found")).toEqual([]);
  });
  test("an empty query shows real recent items and their update date, while recent failures stay errors", () => {
    const render = (failed = false) =>
      renderToStaticMarkup(
        <SearchResults
          recent
          resource={
            failed
              ? {
                  pending: false,
                  error: { kind: "forbidden", retryable: false },
                }
              : {
                  pending: false,
                  data: { items: hits, degraded: false, limit: 20 },
                }
          }
          hits={
            failed
              ? []
              : [
                  {
                    ...hits[0]!,
                    excerpt: "",
                    updatedAt: "2026-10-17T00:00:00Z",
                  },
                ]
          }
          spaces={[{ id: "linux", name: "Linux 运维", path: "ops/linux" }]}
          query=""
          selected="document:one"
          onSelect={() => {}}
          onRetry={() => {}}
          scopeMissing={false}
          onClearScope={() => {}}
          scrollRef={createRef<HTMLDivElement>()}
        />,
      );
    const html = render();
    expect(html).toContain("Recently updated");
    expect(html).toContain("Deployment guide");
    expect(html).toMatch(/datetime="2026-10-17T00:00:00Z"/i);
    expect(html).not.toContain("What are you looking for?");
    expect(html).toContain("Up to 20 items");
    const denied = render(true);
    expect(denied).toContain("Could not load recent updates");
    expect(denied).not.toContain("No content to read yet");
    expect(denied).not.toContain("Deployment guide");
  });
  test("space scope shows names once while retaining exact path values", () => {
    const html = filters("workspace=ops%2Flinux");
    expect(html.match(/>All spaces</g)?.length).toBe(1);
    expect(html.match(/role="combobox"/g)?.length).toBe(1);
    expect(html).toContain('title="Linux 运维"');
    expect(html).toMatch(/<button[^>]*data-state="on"[^>]*title="Linux 运维"/);
    expect(html).not.toContain('id="filter-workspace"');
  });
  test("document-only filtering disables context categories", () => {
    const html = filters("type=documents");
    expect(html).toMatch(/<button[^>]*disabled=""[^>]*id="filter-kind"/);
  });
  test("larger space lists offer a name search without duplicating the all-space scope", () => {
    const html = filters("", {
      spaces: Array.from({ length: 9 }, (_, index) => ({
        id: `space-${index}`,
        name: `Project ${index}`,
        path: `internal/project-${index}`,
      })),
    });
    expect(html).toContain('placeholder="Search space names"');
    expect(html.match(/>All spaces</g)?.length).toBe(1);
    expect(filters()).not.toContain('placeholder="Search space names"');
  });
  test("an unavailable scope remains selected rather than silently broadening the search", () => {
    const html = filters("workspace=missing%2Fspace", { spaces: [] });
    expect(html).toMatch(
      /<button[^>]*data-state="on"[^>]*title="Current space unavailable"/,
    );
    expect(html).not.toContain("missing/space</span>");
  });
  test("workspace loading still offers all-space search and recoverable errors retain known choices", () => {
    const loading = filters("", { spaces: undefined, pending: true });
    expect(loading).toContain('role="status" aria-label="Loading"');
    expect(loading).toContain(">All spaces</span>");
    const failed = filters("", {
      error: { kind: "unavailable", retryable: true },
    });
    expect(failed).toContain('title="Linux 运维"');
    expect(failed).toContain('role="status"');
  });
  test("forbidden workspace refresh hides stale authorized-list names", () => {
    const html = filters("", {
      error: { kind: "forbidden", retryable: false },
    });
    expect(html).not.toContain("Linux 运维");
    expect(html).not.toContain("个人笔记");
    expect(html).toContain(">All spaces</span>");
  });
  test("results report the displayed batch and explain degraded retrieval without replacing results", () => {
    const html = renderToStaticMarkup(
      <SearchResults
        resource={{
          pending: false,
          data: { items: hits, degraded: true, limit: 50 },
        }}
        spaces={[{ id: "linux", name: "Linux 运维", path: "ops/linux" }]}
        degraded
        hits={hits.slice(0, 1)}
        query="guide"
        selected="document:one"
        onSelect={() => {}}
        onRetry={() => {}}
        scopeMissing={false}
        onClearScope={() => {}}
        scrollRef={createRef<HTMLDivElement>()}
      />,
    );
    expect(html).toContain("Showing 1 item");
    expect(html).not.toContain("Showing 1 items");
    expect(html).toContain("50 results");
    expect(html).toContain("Keyword");
    expect(html).toContain("Deployment <mark>guide</mark>");
    expect(html).not.toContain("Backup decision");
    expect(html).toContain('title="Linux 运维"');
    expect(html).not.toContain('title="ops/linux"');
  });
  test("the preview offers only copy and its matching workspace link outside the body", () => {
    const detail = {
      ...hits[0]!,
      content: "# Full document\n\nCanonical Markdown after the excerpt.",
      updatedAt: "2026-10-17T00:00:00Z",
    };
    const render = (path: string) =>
      renderToStaticMarkup(
        <MemoryRouter>
          <SearchPreview
            item={hits[0]}
            resource={{ data: detail, pending: false }}
            workspace={{ id: "linux", name: "Linux 运维", path }}
            onBack={() => {}}
            onRetry={() => {}}
            paneRef={createRef<HTMLElement>()}
          />
        </MemoryRouter>,
      );
    const matching = render("ops/linux");
    expect(matching).not.toContain("More actions");
    expect(matching).not.toContain("Open reader");
    expect(matching).not.toContain('aria-haspopup="menu"');
    expect(matching).toContain('href="/spaces/linux/documents/one"');
    expect(matching.indexOf('href="/spaces/linux/documents/one"')).toBeLessThan(
      matching.indexOf("<article"),
    );
    expect(matching).toContain("Canonical Markdown after the excerpt.");
    expect(matching).toContain("Linux 运维</span>");
    expect(matching).not.toContain("ops/linux</span>");
    expect(render("other")).not.toContain('href="/spaces/linux/documents/one"');
  });
});
