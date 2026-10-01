import { describe, expect, test } from "bun:test";
import { Glob } from "bun";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router";
import { renderToStaticMarkup } from "react-dom/server";
import { Header } from "../../src/components/layout/Header";
import { appRoutes } from "../../src/routes";
import { AppLoading } from "../../src/components/feedback/RouteFeedback";
import { initializeI18n } from "../../src/lib/i18n";
import { KnowledgeEmptyState } from "../../src/features/home/KnowledgeEmptyState";
import { previewWorkspaces } from "../../src/features/home/preview-data";

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
      "{navigation-and-actions,home-overview,knowledge-search,workspace-browser,knowledge-actions,application-settings,ui-states}.json",
    );
    const messages = [];
    for await (const file of glob.scan({ cwd: directory.pathname })) {
      messages.push(await Bun.file(new URL(file, directory)).json());
    }
    return Object.assign({}, ...messages);
  });
}

describe("Application routes", () => {
  test("a direct search link renders its query and matching preview results", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/search?preview=1&q=notes&type=documents"],
    });
    try {
      await ready(router);
      expect(router.state.errors).toBeNull();
      expect(router.state.matches.at(-1)?.route.id).toBe("search");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain('value="notes"');
      expect(html).toContain("Working notes.md");
      expect(html).not.toContain("Architecture overview.md");
    } finally {
      router.dispose();
    }
  });

  test("detail routes select their own record and preserve the source location", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: [
        {
          pathname: "/documents/notes",
          search: "?preview=1",
          state: { from: "/search?preview=1&q=notes" },
        },
      ],
    });
    try {
      await ready(router);
      expect(router.state.errors).toBeNull();
      expect(
        renderToStaticMarkup(<RouterProvider router={router} />),
      ).toContain("Working notes.md");
      expect(router.state.location.state).toEqual({
        from: "/search?preview=1&q=notes",
      });
      await router.navigate("/contexts/decision?preview=1");
      expect(router.state.matches.at(-1)?.route.id).toBe("context");
      expect(
        renderToStaticMarkup(<RouterProvider router={router} />),
      ).toContain("Choose a single source for decisions");
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
    for (const path of ["/", "/?preview=1&state=loading"]) {
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

  test("empty preview explains both collections and keeps the create entry", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/?preview=1&state=empty"],
    });
    try {
      await ready(router);
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).toContain("Create your first space");
      expect(html).toContain("home-create-action");
      expect(html).toContain("Create a space above");
      expect(html).toContain("A home for knowledge worth keeping.");
      expect(html).not.toContain("Most recently updated");
      expect(html).toContain("Start with your first piece of knowledge.");
      expect(html).toContain("Save a piece of context");
      expect(html).toContain("Save a Markdown document");
      expect(html).not.toContain("home-space-skeleton");
      expect(html).not.toContain('href="/spaces/projects?preview=1"');
    } finally {
      router.dispose();
    }
  });

  test("knowledge onboarding uses an existing space and waits for space availability", async () => {
    await english();
    for (const spaces of [previewWorkspaces, [], undefined]) {
      const router = createMemoryRouter([
        {
          Component: () => (
            <Outlet context={{ linkTo: (path: string) => path }} />
          ),
          children: [
            {
              index: true,
              Component: () => <KnowledgeEmptyState spaces={spaces} />,
            },
          ],
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

  test("preview failure does not misrepresent unavailable collections as empty", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/?preview=1&state=error"],
    });
    try {
      await ready(router);
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(html).not.toContain("Create your first space");
      expect(html).not.toContain("Start with your first piece of knowledge.");
      expect(html).not.toContain("home-space-skeleton");
      expect(html).toContain("Retry");
    } finally {
      router.dispose();
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
  test("all menu destinations navigate and preserve preview mode", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/?preview=1"],
    });
    try {
      await ready(router);
      for (const path of ["/", "/search", "/spaces", "/settings"]) {
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(menu(html)).toContain(`href="${path}?preview=1"`);
        await router.navigate(`${path}?preview=1`);
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
        'aria-label="ContextDepot · Home" title="Home" href="/?preview=1"',
      );
    } finally {
      router.dispose();
    }
  });

  test("space details select Spaces and direct knowledge links select Search", async () => {
    await english();
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ["/spaces/personal?preview=1"],
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
        await router.navigate(`${path}?preview=1`);
        expect(
          selected(
            renderToStaticMarkup(<RouterProvider router={router} />),
            "/search",
          ),
        ).toBe(true);
      }
      await router.navigate("/documents/notes?preview=1", {
        state: { from: "/spaces/personal?preview=1", navigation: "spaces" },
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
      initialEntries: ["/search?preview=1&q=notes"],
    });
    try {
      await ready(router);
      expect(
        renderToStaticMarkup(<RouterProvider router={router} />),
      ).toContain("Working notes.md");
      const navigation = router.navigate("/settings?preview=1");
      const html = renderToStaticMarkup(<RouterProvider router={router} />);
      expect(menu(html)).toContain("rail-link pending rail-settings");
      expect(menu(html)).toContain('role="status"');
      expect(html).toContain('aria-busy="true"');
      expect(html).toContain('data-slot="skeleton"');
      expect(html).not.toContain("Working notes.md");
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
      initialEntries: ["/search?preview=1&q=notes"],
    });
    try {
      await ready(router);
      const navigation = router.navigate("/?preview=1");
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
