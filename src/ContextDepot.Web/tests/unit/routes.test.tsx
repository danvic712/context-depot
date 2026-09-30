import { describe, expect, test } from "bun:test";
import { Glob } from "bun";
import { createMemoryRouter, RouterProvider } from "react-router";
import { renderToStaticMarkup } from "react-dom/server";
import { appRoutes } from "../../src/routes";
import { initializeI18n } from "../../src/lib/i18n";

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
