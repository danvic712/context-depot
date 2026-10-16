import { describe, expect, test } from "bun:test";
import { Glob } from "bun";
import type { ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { createMemoryRouter, RouterProvider } from "react-router";
import { initializeI18n } from "../../src/lib/i18n";
import { normalizeKnowledgeParams } from "../../src/features/knowledge/query-params";
import { WorkspaceRow } from "../../src/features/spaces/WorkspaceRow";
import { SpaceNavigation } from "../../src/features/spaces/SpaceBrowser";
import type { useSpaceDirectory } from "../../src/features/spaces/use-space-directory";
import type { Space } from "../../src/features/spaces/spaces-api";

const space: Space = {
  id: "research",
  name: "Research",
  path: "research",
  description: null,
  contextCount: 4,
  documentCount: 2,
  subspaceCount: 1,
  activityAt: "2026-10-03T08:00:00Z",
};
function directory(
  items: Space[],
  totalCount = items.length,
  page = 1,
): ReturnType<typeof useSpaceDirectory> {
  return {
    data: { asOf: space.activityAt, items, totalCount, page, pageSize: 12 },
    pending: false,
    page,
    changePage: () => {},
    refresh: () => {},
    onCreated: () => {},
  };
}
async function render(path: string, content: ReactNode) {
  const files = new Glob(
    "{navigation-and-actions,home-overview,knowledge-search,workspace-browser,application-settings,ui-states}.json",
  );
  const locale = new URL("../../../../locales/en-US/", import.meta.url);
  await initializeI18n("en", async () => {
    const messages = [];
    for await (const file of files.scan({ cwd: locale.pathname }))
      messages.push(await Bun.file(new URL(file, locale)).json());
    return Object.assign({}, ...messages);
  });
  const router = createMemoryRouter([{ path: "*", Component: () => content }], {
    initialEntries: [path],
  });
  try {
    return renderToStaticMarkup(<RouterProvider router={router} />);
  } finally {
    router.dispose();
  }
}

describe("Space browser navigation", () => {
  test("legacy preview cleanup preserves the reading selection and independent pages", () => {
    const params = normalizeKnowledgeParams(
      new URLSearchParams(
        "view=preview&preview=1&selected=document:notes&directoryPage=3&knowledgePage=4&knowledgeLayout=cards",
      ),
    );
    expect(params.toString()).toBe(
      "selected=document%3Anotes&directoryPage=3&knowledgePage=4&knowledgeLayout=cards",
    );
  });
  test("a space selection carries its directory page into the detail route", async () => {
    const html = await render("/spaces?page=3", <WorkspaceRow space={space} />);
    expect(html).toContain('href="/spaces/research?directoryPage=3"');
    expect(html).not.toContain("subspace");
  });
  test("space switcher links preserve the directory page and omit the previous knowledge selection", async () => {
    const html = await render(
      "/spaces/research?directoryPage=3&knowledgePage=2&selected=document:notes",
      <SpaceNavigation
        roots={directory([space], 25, 3)}
        selectedSpaceId={space.id}
      />,
    );
    expect(html).toContain('href="/spaces?page=3"');
    expect(html).toContain('href="/spaces/research?directoryPage=3"');
    expect(html).toContain('aria-current="page"');
    expect(html).not.toContain("subspace");
    expect(html).not.toContain("selected=");
  });
  test("a forbidden refresh hides the previous directory names", async () => {
    const roots = {
      ...directory([space]),
      error: { kind: "forbidden" as const, retryable: false },
    };
    const html = await render("/spaces", <SpaceNavigation roots={roots} />);
    expect(html).not.toContain('href="/spaces/research"');
    expect(html).not.toContain("Research");
  });
});
