import { describe, expect, test } from "bun:test";
import { Glob } from "bun";
import { createMemoryRouter, RouterProvider } from "react-router";
import { renderToStaticMarkup } from "react-dom/server";
import { Header } from "../../src/components/layout/Header";
import { appRoutes } from "../../src/routes";
import { AppLoading } from "../../src/components/feedback/RouteFeedback";
import { initializeI18n } from "../../src/lib/i18n";
import { KnowledgeEmptyState } from "../../src/features/home/KnowledgeEmptyState";
import { WorkspaceTiles } from "../../src/features/home/HomeCollections";
import type { WorkspaceSummary } from "../../src/features/home/home-api";

const workspaces: WorkspaceSummary[] = [
  {
    id: "projects",
    name: "Projects",
    description: null,
    path: "projects",
    contextCount: 0,
    documentCount: 0,
    activityAt: "2026-10-01T00:00:00Z",
  },
];

async function ready(router: ReturnType<typeof createMemoryRouter>) {
  if (router.state.initialized && router.state.navigation.state === "idle")
    return;
  await new Promise<void>((resolve) => {
    const unsubscribe = router.subscribe((state) => {
      if (state.initialized && state.navigation.state === "idle") {
        unsubscribe();
        resolve();
      }
    });
  });
}

async function english() {
  await initializeI18n("en", async () => {
    const directory = new URL("../../../../locales/en-US/", import.meta.url);
    const glob = new Glob(
      "{navigation-and-actions,home-overview,knowledge-search,workspace-browser,application-settings,ui-states}.json",
    );
    const messages = [];
    for await (const file of glob.scan({ cwd: directory.pathname })) {
      messages.push(await Bun.file(new URL(file, directory)).json());
    }
    return Object.assign({}, ...messages);
  });
}

describe("Application routes", () => {
  test("a direct search link preserves its query and filters while results load", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/search?q=notes&type=documents"],
    });
    try {
      await ready(router);
      expect(router.state.errors).toBeNull();
      expect(router.state.matches.at(-1)?.route.id).toBe("search");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain('value="notes"');
      expect(html).toContain('data-slot="skeleton"');
      expect(html).toContain(
        'aria-label="Filter types in this returned batch"',
      );
      expect(html).not.toContain("Working notes.md");
      expect(html).not.toContain("Architecture overview.md");
    } finally {
      router.dispose();
    }
  });

  test("detail routes load knowledge and preserve a deterministic return to the source", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: [
        {
          pathname: "/documents/notes",
          state: { from: "/search?q=notes" },
        },
      ],
    });
    try {
      await ready(router);
      expect(router.state.errors).toBeNull();
      const document = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(document).toContain('href="/search?q=notes"');
      expect(document).toContain('class="knowledge-reader" aria-busy="true"');
      expect(document).toContain('data-slot="skeleton"');
      expect(document).not.toContain("Recent knowledge needs a Web read API.");
      expect(router.state.location.state).toEqual({
        from: "/search?q=notes",
      });
      await router.navigate("/contexts/decision");
      expect(router.state.matches.at(-1)?.route.id).toBe("context");
      const context = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(context).toContain('href="/search"');
      expect(context).toContain('class="knowledge-reader" aria-busy="true"');
      expect(context).not.toContain("Recent knowledge needs a Web read API.");
    } finally {
      router.dispose();
    }
  });
});

function menu(html: string) {
  return (
    html.match(
      /<nav[^>]*aria-label="Primary navigation"[^>]*>(.*?)<\/nav>/s,
    )?.[1] ?? ""
  );
}

describe("Home collection states", () => {
  test("initial loading announces progress and shows placeholders before empty content", async () => {
    await english();
    for (const path of ["/", "/?preview=1&state=empty"]) {
      const router = createMemoryRouter(appRoutes, { initialEntries: [path] });
      try {
        await ready(router);
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(html).toContain('role="status">Loading');
        expect(html.match(/aria-busy="true"/g)).toHaveLength(2);
        expect(html.match(/home-space-skeleton/g)).toHaveLength(4);
        expect(html).not.toContain("Create your first space");
        expect(html).not.toContain("Start with your first piece of knowledge.");
        expect(html).not.toContain("Continue with the knowledge you’ve saved.");
        expect(html).not.toContain("home-create-tile");
        expect(html).toContain('role="search"');
      } finally {
        router.dispose();
      }
    }
  });

  test("an empty workspace collection keeps the create entry", async () => {
    await english();
    const router = createMemoryRouter([
      {
        path: "/",
        Component: () => (
          <WorkspaceTiles
            items={[]}
            pending={false}
            empty
            onCreated={() => {}}
          />
        ),
      },
    ]);
    try {
      await ready(router);
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain("Create your first space");
      expect(html).toContain("home-create-action");
      expect(html).not.toContain("home-space-skeleton");
    } finally {
      router.dispose();
    }
  });

  test("knowledge onboarding uses an existing space and waits for space availability", async () => {
    await english();
    for (const spaces of [workspaces, [], undefined]) {
      const router = createMemoryRouter([
        {
          path: "/",
          Component: () => <KnowledgeEmptyState spaces={spaces} />,
        },
      ]);
      try {
        await ready(router);
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        if (spaces?.length) {
          expect(html).toContain('href="/spaces/projects"');
          expect(html).toContain(
            "Ask your agent to save knowledge to Projects (projects)",
          );
          expect(html).not.toContain("Create a space above");
        } else {
          expect(html).not.toContain("Open space");
          expect(html).toContain(
            spaces
              ? "Create a space above"
              : "Connect an MCP client and save knowledge",
          );
        }
      } finally {
        router.dispose();
      }
    }
  });
});

function selected(html: string, path: string) {
  const links = [...menu(html).matchAll(/<a\b([^>]+)>/g)];
  return links.some(([, attributes]) => {
    const href = attributes.match(/href="([^"]+)"/)?.[1];
    return (
      href?.split("?")[0] === path && attributes.includes('aria-current="page"')
    );
  });
}

describe("Sidebar navigation", () => {
  test("legacy sample links never expose a sample toggle or simulation controls", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/?preview=1&state=empty"],
    });
    try {
      await ready(router);
      for (const path of [
        "/",
        "/search?q=notes",
        "/spaces",
        "/spaces/projects",
        "/documents/notes",
        "/contexts/decision",
        "/settings",
      ]) {
        await router.navigate(
          `${path}${path.includes("?") ? "&" : "?"}preview=1&state=error`,
        );
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(html).not.toContain("preview-control");
        expect(html).not.toContain("Preview sample data");
        expect(html).not.toContain("Showing sample data");
        expect(html).not.toContain("Working notes.md");
        expect(html).not.toContain("Simulate hash conflict");
        expect(menu(html)).not.toContain("preview=1");
      }
    } finally {
      router.dispose();
    }
  });
  test("all menu destinations navigate without sample-mode controls", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/"],
    });
    try {
      await ready(router);
      for (const path of ["/", "/search", "/spaces", "/settings"]) {
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(menu(html)).toContain(`href="${path}"`);
        await router.navigate(`${path}`);
        expect(router.state.errors).toBeNull();
        expect(
          selected(
            renderToStaticMarkup(<RouterProvider router={router} />),
            path,
          ),
        ).toBe(true);
      }
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(selected(html, "/")).toBe(false);
      expect(menu(html)).not.toContain("Timeline");
      expect(html).toContain('class="rail-brand-link"');
      expect(html).toContain(
        'aria-label="ContextDepot · Home" title="Home" href="/"',
      );
    } finally {
      router.dispose();
    }
  });

  test("space details select Spaces and direct knowledge links select Search", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/spaces/personal"],
    });
    try {
      await ready(router);
      expect(
        selected(
          renderToStaticMarkup(<RouterProvider router={router} />),
          "/spaces",
        ),
      ).toBe(true);
      for (const path of ["/documents/notes", "/contexts/decision"]) {
        await router.navigate(`${path}`);
        expect(
          selected(
            renderToStaticMarkup(<RouterProvider router={router} />),
            "/search",
          ),
        ).toBe(true);
      }
      await router.navigate("/documents/notes", {
        state: { from: "/spaces/personal", navigation: "spaces" },
      });
      expect(
        selected(
          renderToStaticMarkup(<RouterProvider router={router} />),
          "/spaces",
        ),
      ).toBe(true);
      await router.navigate(-1);
      expect(
        selected(
          renderToStaticMarkup(<RouterProvider router={router} />),
          "/search",
        ),
      ).toBe(true);
    } finally {
      router.dispose();
    }
  });

  test("pending navigation shows feedback without rendering old page content", async () => {
    await english();
    let release!: () => void;
    const deferred = new Promise<void>((resolve) => {
      release = resolve;
    });
    const routes = appRoutes.map((route) => ({
      ...route,
      children: route.children?.map((child) =>
        child.id === "settings" ? { ...child, loader: () => deferred } : child,
      ),
    }));
    const router = createMemoryRouter(routes, {
      initialEntries: ["/search?q=notes"],
    });
    try {
      await ready(router);
      expect(
        renderToStaticMarkup(<RouterProvider router={router} />),
      ).toContain('class="search-workbench"');
      const navigation = router.navigate("/settings");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(menu(html)).toContain("rail-link pending rail-settings");
      expect(menu(html)).toContain('role="status"');
      expect(html).toContain('aria-busy="true"');
      expect(html).toContain('data-slot="skeleton"');
      expect(html).not.toContain('class="search-workbench"');
      release();
      await navigation;
      const loaded = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(selected(loaded, "/settings")).toBe(true);
      expect(menu(loaded)).not.toContain("pending");
    } finally {
      release();
      router.dispose();
    }
  });

  test("startup skeleton provides navigation and page placeholders", () => {
    const html = renderToStaticMarkup(<AppLoading pathname="/" />);
    expect(html).toContain('class="rail"');
    expect(html).toContain('aria-busy="true"');
    expect(html).toContain('role="status"');
    expect(html).toContain('data-slot="skeleton"');
    expect(html).toContain('class="frame frame-home"');
    expect(html).toContain('class="page home"');
    expect(html).toContain('class="home-hero"');
    expect(html).toContain('class="home-dashboard"');
    expect(html).toContain('class="home-guide-content"');
    expect(html.match(/home-space-skeleton/g)).toHaveLength(4);
    expect(html).not.toContain("<button");
  });

  test("startup on another route does not show the home layout", () => {
    const html = renderToStaticMarkup(<AppLoading pathname="/settings" />);
    expect(html).not.toContain("home-dashboard");
    expect(html).toContain('role="status"');
  });

  test("direct knowledge startup shows a reading skeleton before translations initialize", () => {
    for (const pathname of ["/contexts/record", "/documents/record"]) {
      const html = renderToStaticMarkup(<AppLoading pathname={pathname} />);
      expect(html).toContain('class="knowledge-reader"');
      expect(html).toContain('aria-label="Loading / 加载中"');
      expect(html).not.toContain("home-dashboard");
      expect(html).not.toContain("<button");
    }
  });

  test("startup on Search preserves its filters and two-pane layout", () => {
    const html = renderToStaticMarkup(<AppLoading pathname="/search" />);
    expect(html).toContain('class="page search"');
    expect(html).toContain("search-page-skeleton");
    expect(html).toContain('class="search-hero"');
    expect(html).toContain('class="search-box"');
    expect(html).toContain("search-type-field");
    expect(html).toContain("search-kind-field");
    expect(html).toContain("search-workspace-field");
    expect(html).toContain('class="search-panes"');
    expect(html).toContain('class="search-results-pane"');
    expect(html).toContain('class="search-preview-pane"');
    expect(html.match(/search-result-skeleton/g)).toHaveLength(3);
    expect(html).not.toContain("<button");
  });

  test("navigation to Search uses its layout while the destination loads", async () => {
    await english();
    let release!: () => void;
    const deferred = new Promise<null>((resolve) => {
      release = () => resolve(null);
    });
    const routes = appRoutes.map((route) => ({
      ...route,
      children: route.children?.map((child) =>
        child.id === "search" ? { ...child, loader: () => deferred } : child,
      ),
    }));
    const router = createMemoryRouter(routes, {
      initialEntries: ["/"],
    });
    try {
      await ready(router);
      const navigation = router.navigate("/search?q=notes");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain('class="page search"');
      expect(html).toContain("search-page-skeleton");
      expect(html).toContain('aria-label="Loading"');
      expect(html).not.toContain("home-dashboard");
      release();
      await navigation;
      const loaded = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(loaded).not.toContain("search-page-skeleton");
      expect(loaded).toContain('class="search-workbench"');
    } finally {
      release();
      router.dispose();
    }
  });

  test("navigation to home uses the home skeleton before the destination loads", async () => {
    await english();
    let release!: () => void;
    const deferred = new Promise<null>((resolve) => {
      release = () => resolve(null);
    });
    const routes = appRoutes.map((route) => ({
      ...route,
      children: route.children?.map((child) =>
        child.id === "home" ? { ...child, loader: () => deferred } : child,
      ),
    }));
    const router = createMemoryRouter(routes, {
      initialEntries: ["/search?q=notes"],
    });
    try {
      await ready(router);
      const navigation = router.navigate("/");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain('class="page home"');
      expect(html).toContain('class="home-skeleton"');
      expect(html).toContain('aria-label="Loading"');
      expect(html).not.toContain("Working notes.md");
      release();
      await navigation;
      const loaded = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(loaded).not.toContain('class="home-skeleton"');
      expect(loaded).toContain('class="home-dashboard"');
    } finally {
      release();
      router.dispose();
    }
  });
});

describe("Header preferences", () => {
  test("settings expose explicit browser preferences without deployment defaults", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/settings"],
    });
    try {
      await ready(router);
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain('aria-label="Theme: System"');
      expect(html).toContain('aria-label="Language: English"');
      expect(html).not.toContain("Use deployment default");
      expect(html).not.toContain("Following deployment default");
    } finally {
      router.dispose();
    }
  });

  test("language loading announces status and prevents duplicate selection", async () => {
    await english();
    const html = renderToStaticMarkup(
      <Header
        theme="dark"
        lang="en"
        languagePending
        onTheme={() => {}}
        onLanguage={() => {}}
      />,
    );
    const button = html.match(
      /<button[^>]*aria-label="Language: Loading"[^>]*>/,
    )?.[0];
    expect(button).toBeDefined();
    expect(button).toContain('disabled=""');
    expect(button).toContain('aria-busy="true"');
    expect(html).toContain('role="status"');
    expect(html).toContain('aria-label="Theme: Dark"');
  });
});
