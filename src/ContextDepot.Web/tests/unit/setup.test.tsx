import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { LoaderFunctionArgs } from "react-router";
import { httpClient } from "../../src/lib/http-client";
import { appRoutes } from "../../src/routes";
import { initializeI18n, type Messages } from "../../src/lib/i18n";
import { AppLoading } from "../../src/components/feedback/RouteFeedback";
import {
  completeSetup,
  currentSetupStep,
  getSetup,
  parseSetup,
  parseSetupCompletion,
  type SetupStatus,
} from "../../src/features/setup/setup-api";
import { setupLoader } from "../../src/features/setup/setup-loader";
import { mcpClientConfiguration } from "../../src/features/setup/mcp-configuration";
import { setupErrorMessage } from "../../src/features/setup/setup-error";
import { AxiosError, AxiosHeaders } from "axios";

const workspace = {
  id: "initial-workspace",
  name: "Research",
  path: "research",
  description: null,
};
const pending: SetupStatus = {
  state: "pending",
  workspace: null,
  nextStep: "workspace",
  mcpPath: "/mcp",
};
const progress: SetupStatus = {
  state: "inProgress",
  workspace,
  nextStep: "inference",
  mcpPath: "/mcp",
};
const completed: SetupStatus = {
  state: "completed",
  workspace: null,
  nextStep: "review",
  mcpPath: "/mcp",
};

async function english() {
  await initializeI18n("en", async () => {
    const files = [
      "navigation-and-actions",
      "home-overview",
      "knowledge-search",
      "workspace-browser",
      "application-settings",
      "ui-states",
    ];
    const messages = await Promise.all(
      files.map((name) =>
        Bun.file(
          new URL(`../../../../locales/en-US/${name}.json`, import.meta.url),
        ).json(),
      ),
    );
    return Object.assign({}, ...messages) as Messages;
  });
}
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
async function withServer(
  run: (context: {
    requests: {
      path: string;
      method: string;
      body: unknown;
      management: string | null;
    }[];
    setStatus: (status: SetupStatus) => void;
    fail: () => void;
  }) => Promise<void>,
) {
  let status = pending;
  let unavailable = false;
  const requests: {
    path: string;
    method: string;
    body: unknown;
    management: string | null;
  }[] = [];
  const server = Bun.serve({
    port: 0,
    async fetch(request) {
      const path = new URL(request.url).pathname;
      const body: unknown =
        ["POST", "PUT"].includes(request.method) &&
        request.headers.get("content-type")?.includes("json")
          ? await request.json()
          : null;
      requests.push({
        path,
        method: request.method,
        body,
        management: request.headers.get("X-ContextDepot-Management"),
      });
      if (unavailable)
        return Response.json(
          { code: "SetupUnavailable", detail: "internal-secret-diagnostic" },
          { status: 503 },
        );
      return Response.json(
        path === "/api/setup/complete" ? { status, accessKey: null } : status,
      );
    },
  });
  const interceptor = httpClient.interceptors.request.use((config) => {
    config.baseURL = `${server.url}api`;
    return config;
  });
  try {
    await run({
      requests,
      setStatus: (value) => {
        status = value;
      },
      fail: () => {
        unavailable = true;
      },
    });
  } finally {
    httpClient.interceptors.request.eject(interceptor);
    server.stop(true);
  }
}

describe("First-run setup contracts", () => {
  test("unknown and inherited error codes use the localized fallback", () => {
    for (const code of ["__proto__", "toString", "unknown"]) {
      const error = new AxiosError("Request failed");
      error.response = {
        data: { code },
        status: 409,
        statusText: "Conflict",
        headers: {},
        config: { headers: new AxiosHeaders() },
      };
      expect(setupErrorMessage(error)).toBe("setupSaveError");
    }
  });
  test("rejects inconsistent states and untrusted MCP addresses instead of assuming setup is complete", () => {
    for (const data of [
      { ...pending, state: "unknown" },
      { ...pending, state: { toString: () => "completed" } },
      { ...pending, nextStep: "review" },
      { ...pending, workspace },
      { ...progress, workspace: null },
      { ...completed, nextStep: "inference" },
      { ...progress, workspace: { ...workspace, description: 1 } },
      { ...pending, mcpPath: "https://external.test/mcp" },
    ])
      expect(() => parseSetup(data)).toThrow();
    expect(parseSetup(pending)).toEqual(pending);
    expect(parseSetup(completed)).toEqual(completed);
    expect(
      parseSetup({
        ...progress,
        secret: "private",
        workspace: { ...workspace, secret: "private" },
      }),
    ).not.toHaveProperty("secret");
    expect(
      parseSetup({
        ...progress,
        workspace: { ...workspace, secret: "private" },
      }).workspace,
    ).not.toHaveProperty("secret");
  });
  test("restores saved progress and only accepts URL steps that have been reached", () => {
    expect(currentSetupStep(pending, "review")).toBe("workspace");
    expect(currentSetupStep(progress, null)).toBe("inference");
    expect(currentSetupStep(progress, "accessKey")).toBe("inference");
    expect(currentSetupStep(progress, "workspace")).toBe("workspace");
    expect(
      currentSetupStep({ ...progress, nextStep: "review" }, "accessKey"),
    ).toBe("accessKey");
    expect(
      currentSetupStep({ ...progress, nextStep: "accessKey" }, "unknown"),
    ).toBe("accessKey");
  });

  test("only final confirmation writes the complete draft through the management boundary", async () => {
    await withServer(async ({ requests, setStatus }) => {
      const signal = new AbortController().signal;
      expect(await getSetup(signal)).toEqual(pending);
      setStatus(completed);
      const draft = {
        workspace: { name: "Research", path: "research", description: "" },
        providers: [],
        accessKeyName: "Client",
      };
      expect(await completeSetup(draft, signal)).toEqual({
        status: completed,
        accessKey: null,
      });
      expect(requests.map((request) => [request.method, request.path])).toEqual(
        [
          ["GET", "/api/setup"],
          ["POST", "/api/setup/complete"],
        ],
      );
      expect(requests[1]?.body).toEqual(draft);
      expect(requests[1]?.management).toBe("web");
    });
  });
  test("cancellation does not send a final initialization write", async () => {
    await withServer(async ({ requests }) => {
      const controller = new AbortController();
      controller.abort();
      await expect(
        completeSetup(
          {
            workspace: { name: "Research", path: "research", description: "" },
            providers: [],
            accessKeyName: null,
          },
          controller.signal,
        ),
      ).rejects.toThrow();
      expect(requests).toHaveLength(0);
    });
  });
  test("completion rejects uncommitted or unrelated key responses", () => {
    expect(() =>
      parseSetupCompletion({ status: pending, accessKey: null }),
    ).toThrow();
    expect(() =>
      parseSetupCompletion({ status: completed, accessKey: {} }),
    ).toThrow();
    expect(
      parseSetupCompletion({
        status: completed,
        accessKey: null,
        secret: "unexpected",
      }),
    ).not.toHaveProperty("secret");
  });
  test("MCP examples use the fixed key header and keep a placeholder after one-time secret display", () => {
    const configuration = JSON.parse(
      mcpClientConfiguration("https://depot.test/mcp", "one-time-key"),
    );
    expect(configuration.mcpServers.contextdepot).toEqual({
      url: "https://depot.test/mcp",
      headers: { "X-ContextDepot-Key": "one-time-key" },
    });
    expect(mcpClientConfiguration("https://depot.test/mcp")).not.toContain(
      "one-time-key",
    );
    expect(mcpClientConfiguration("https://depot.test/mcp")).toContain(
      "<YOUR_ACCESS_KEY>",
    );
  });
});

describe("First-run route gate", () => {
  test("redirects incomplete installations to setup before business pages mount", async () => {
    await withServer(async ({ requests }) => {
      const result = await setupLoader({
        request: new Request("https://depot.test/documents/record"),
      } as LoaderFunctionArgs);
      expect(result).toBeInstanceOf(Response);
      expect((result as Response).headers.get("Location")).toBe("/setup");
      expect(requests.map((request) => request.path)).toEqual(["/api/setup"]);
    });
  });
  test("redirects completed installations away from setup, including upgraded instances without an initial Workspace", async () => {
    await withServer(async ({ setStatus }) => {
      setStatus(completed);
      const result = await setupLoader({
        request: new Request("https://depot.test/setup?step=workspace"),
      } as LoaderFunctionArgs);
      expect((result as Response).headers.get("Location")).toBe("/");
    });
  });
  test("renders the first-run layout with both appearance menus and blocks direct links to future stages", async () => {
    await english();
    await withServer(async ({ requests }) => {
      const router = createMemoryRouter(appRoutes, {
        initialEntries: ["/setup?step=review"],
      });
      try {
        await ready(router);
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(html).toContain('class="setup-sidebar"');
        expect(html).toContain('aria-current="step"');
        expect(html).toContain('aria-label="Theme: System"');
        expect(html).toContain('aria-label="Language: English"');
        expect(html).toContain("Create your first space");
        expect(html).toContain('id="setup-space-name"');
        expect(html).not.toContain('aria-label="Primary navigation"');
        expect(html).not.toContain("Open ContextDepot");
        expect(requests.every((request) => request.path === "/api/setup")).toBe(
          true,
        );
      } finally {
        router.dispose();
      }
    });
  });
  test("a failed status read shows one initialization error with appearance controls and retry", async () => {
    await english();
    await withServer(async ({ fail }) => {
      fail();
      const router = createMemoryRouter(appRoutes, {
        initialEntries: ["/spaces"],
      });
      try {
        await ready(router);
        const html = renderToStaticMarkup(<RouterProvider router={router} />);
        expect(html).toContain("Could not load your settings");
        expect(html).toContain('aria-label="Theme: System"');
        expect(html).toContain('aria-label="Language: English"');
        expect(html).toContain(">Retry</span>");
        expect(html.match(/role="alert"/g)?.length).toBe(1);
        expect(html.match(/Try again in a moment/g)?.length).toBe(1);
        expect(html).not.toContain("Open your knowledge space");
        expect(html).not.toContain("Unable to check setup status");
        expect(html).not.toContain("Display settings could not be loaded");
        expect(html).not.toContain("Preparing your knowledge space");
        expect(html).not.toContain("Any settings already saved");
        expect(html).not.toContain('aria-label="Primary navigation"');
        expect(html).not.toContain("internal-secret-diagnostic");
        expect(html).not.toContain('class="space-card');
      } finally {
        router.dispose();
      }
    });
  });
  test("setup startup announces loading in its own layout before translations are ready", () => {
    const html = renderToStaticMarkup(<AppLoading pathname="/setup" />);
    expect(html).toContain('class="setup-layout"');
    expect(html).toContain('role="status"');
    expect(html).not.toContain('class="rail"');
    expect(html).not.toContain("home-dashboard");
    expect(html).not.toContain("<button");
  });
});
