import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  test,
} from "bun:test";
import { Window } from "happy-dom";
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { SessionMemorySettings } from "../../src/features/settings/SessionMemorySettings";
import { httpClient } from "../../src/lib/http-client";
import { initializeI18n } from "../../src/lib/i18n";

const window = new Window({ url: "http://localhost/settings" });
const originalGlobals = new Map<string, PropertyDescriptor | undefined>();
let root: Root;
let container: HTMLElement;
let enabled = true;
let getFails = false;
let putFails = false;
let persistFailedPut = false;
let getCount = 0;
let puts: boolean[] = [];
let getGate: Promise<void> | undefined;
let putGate: Promise<void> | undefined;
let releaseGet: (() => void) | undefined;
let releasePut: (() => void) | undefined;
function holdGet() {
  getGate = new Promise((resolve) => {
    releaseGet = resolve;
  });
}
function holdPut() {
  putGate = new Promise((resolve) => {
    releasePut = resolve;
  });
}
const server = Bun.serve({
  port: 0,
  async fetch(request) {
    if (request.method === "PUT") {
      const body = (await request.json()) as { enabled: boolean };
      puts.push(body.enabled);
      await putGate;
      if (!putFails || persistFailedPut) enabled = body.enabled;
      if (putFails) return new Response(null, { status: 503 });
    } else {
      getCount++;
      await getGate;
      if (getFails) return new Response(null, { status: 503 });
    }
    return Response.json({ enabled });
  },
});
const baseURL = httpClient.defaults.baseURL;

async function language(lang: "en" | "zh") {
  await initializeI18n(lang, async (requested) => {
    const locale = requested === "zh" ? "zh-CN" : "en-US";
    return Object.assign(
      {},
      ...(await Promise.all(
        ["application-settings", "ui-states", "navigation-and-actions"].map(
          (file) =>
            Bun.file(
              new URL(
                `../../../../locales/${locale}/${file}.json`,
                import.meta.url,
              ),
            ).json(),
        ),
      )),
    );
  });
}
beforeAll(() => {
  for (const [key, value] of Object.entries({
    window,
    document: window.document,
    navigator: window.navigator,
    HTMLElement: window.HTMLElement,
    HTMLButtonElement: window.HTMLButtonElement,
    HTMLFormElement: window.HTMLFormElement,
    Node: window.Node,
    getComputedStyle: window.getComputedStyle.bind(window),
    IS_REACT_ACT_ENVIRONMENT: true,
  })) {
    originalGlobals.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, {
      configurable: true,
      writable: true,
      value,
    });
  }
  httpClient.defaults.baseURL = `${server.url}api`;
});
afterEach(async () => {
  releaseGet?.();
  releasePut?.();
  if (root) await act(() => root.unmount());
  container?.remove();
  enabled = true;
  getFails = false;
  putFails = false;
  persistFailedPut = false;
  puts = [];
  getCount = 0;
  getGate = undefined;
  putGate = undefined;
  releaseGet = undefined;
  releasePut = undefined;
});
afterAll(() => {
  httpClient.defaults.baseURL = baseURL;
  server.stop(true);
  for (const [key, descriptor] of originalGlobals) {
    if (descriptor) Object.defineProperty(globalThis, key, descriptor);
    else Reflect.deleteProperty(globalThis, key);
  }
  void window.happyDOM.close();
});
async function mount(lang: "en" | "zh" = "en") {
  await language(lang);
  container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
  await act(() => root.render(<SessionMemorySettings />));
}
async function until(check: () => boolean) {
  for (let i = 0; i < 100; i++) {
    if (check()) return;
    await act(async () => {
      await Bun.sleep(10);
    });
  }
  throw new Error(`UI condition not reached: ${container.innerHTML}`);
}
const control = () =>
  container.querySelector<HTMLButtonElement>('[role="switch"]');
async function click(element: HTMLElement) {
  await act(() => element.click());
}
const retry = () =>
  Array.from(container.querySelectorAll("button")).find((button) =>
    button.textContent?.includes("Retry"),
  )!;

describe("Session memory settings", () => {
  test("waits for the authoritative initial enabled value instead of guessing a default", async () => {
    holdGet();
    await mount();
    expect(control()).toBeNull();
    expect(container.textContent).toContain("Checking whether this is enabled");
    expect(container.querySelector('[aria-label="Loading"]')).not.toBeNull();
    releaseGet!();
    await until(() => control() !== null);
    expect(control()!.getAttribute("aria-checked")).toBe("true");
    expect(
      control()!.getAttribute("aria-labelledby") ?? control()!.id,
    ).toBeTruthy();
    expect(
      container.querySelector('label[for="session-memory-enabled"]')
        ?.textContent,
    ).toBe("Enable session memory");
    expect(puts).toEqual([]);
  });

  test("keeps scope with the switch and separates lifecycle guidance from setup requirements", async () => {
    await mount();
    await until(() => control() !== null);
    const scope = container.querySelector("#session-memory-scope");
    const toggle = container.querySelector(".settings-session-memory-toggle");
    expect(toggle?.contains(scope)).toBe(true);
    expect(control()!.getAttribute("aria-describedby")?.split(" ")).toEqual([
      "session-memory-state",
      "session-memory-scope",
    ]);
    const disabled = container.querySelector(
      'section[aria-labelledby="session-memory-disabled-title"]',
    );
    const requirements = container.querySelector(
      'section[aria-labelledby="session-memory-requirements-title"]',
    );
    expect(disabled?.querySelector("h3")?.textContent).toBe("When disabled");
    expect(disabled?.textContent).toContain(
      "only conversations still within their storage time limit",
    );
    expect(requirements?.querySelector("h3")?.textContent).toBe(
      "Before you start",
    );
    expect(requirements?.textContent).toContain("Chat model");
    const link = requirements?.querySelector('a[href="#inference"]');
    expect(link?.textContent).toBe("Set up chat model");
    expect(link?.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
    expect(disabled?.querySelector("a")).toBeNull();
    expect(container.querySelector(".settings-inline-note")).toBeNull();
  });

  test("auto-saves both toggles and blocks duplicate input while saving", async () => {
    await mount();
    await until(() => control() !== null);
    holdPut();
    // Two events in one React batch also exercise the synchronous request guard.
    await act(() => {
      control()!.click();
      control()!.click();
    });
    await until(() => puts.length === 1);
    expect(control()!.disabled).toBe(true);
    expect(container.textContent).toContain("Saving");
    await click(control()!);
    expect(puts).toEqual([false]);
    releasePut!();
    await until(
      () =>
        control()?.getAttribute("aria-checked") === "false" &&
        !control()!.disabled,
    );
    await click(control()!);
    await until(
      () =>
        control()?.getAttribute("aria-checked") === "true" &&
        !control()!.disabled,
    );
    expect(puts).toEqual([false, true]);
    expect(enabled).toBe(true);
  });

  test("initial load failure exposes retry and never offers a guessed switch", async () => {
    getFails = true;
    await mount();
    await until(() => retry() !== undefined);
    expect(control()).toBeNull();
    expect(container.querySelector('[role="alert"]')).not.toBeNull();
    getFails = false;
    await click(retry());
    await until(() => control() !== null);
    expect(control()!.getAttribute("aria-checked")).toBe("true");
    expect(getCount).toBe(2);
  });

  test("failed save reloads an authoritative persisted result before allowing another toggle", async () => {
    enabled = false;
    await mount();
    await until(() => control() !== null);
    expect(control()!.getAttribute("aria-checked")).toBe("false");
    putFails = true;
    persistFailedPut = true;
    holdGet();
    await click(control()!);
    await until(() => getCount === 2);
    expect(control()).toBeNull();
    expect(container.textContent).toContain("Your change may already be saved");
    expect(container.textContent).toContain("Checking whether this is enabled");
    releaseGet!();
    await until(() => control() !== null);
    expect(control()!.getAttribute("aria-checked")).toBe("true");
    expect(control()!.disabled).toBe(false);
    expect(puts).toEqual([true]);
  });

  test("failed reconciliation stays blocked and retry reloads without repeating the uncertain PUT", async () => {
    await mount();
    await until(() => control() !== null);
    putFails = true;
    getFails = true;
    await click(control()!);
    await until(
      () => getCount === 2 && retry() !== undefined && !retry().disabled,
    );
    expect(control()).toBeNull();
    getFails = false;
    await click(retry());
    await until(() => control() !== null);
    expect(control()!.getAttribute("aria-checked")).toBe("true");
    expect(getCount).toBe(3);
    expect(puts).toEqual([false]);
  });

  test("both locales explain global live scope, paused expiry, saved memory and client requirements with an inference link", async () => {
    for (const lang of ["en", "zh"] as const) {
      if (lang === "en") {
        await mount(lang);
        await until(() => control() !== null);
      } else
        await act(async () => {
          await language(lang);
        });
      const text = container.textContent!;
      for (const part of lang === "en"
        ? [
            "Session memory",
            "When disabled",
            "Before you start",
            "every app connected here",
            "No restart",
            "New conversations will no longer be recorded",
            "waiting to be turned into memories will be paused",
            "Work already started may still save memories",
            "Existing memories will not be deleted",
            "Pausing does not extend that limit",
            "only conversations still within their storage time limit",
            "Chat model",
            "turn on conversation recording",
            "This switch alone will not make your assistant start recording",
          ]
        : [
            "会话记忆",
            "关闭后",
            "使用前",
            "所有连接到这里的应用",
            "无需重新启动",
            "不再记录新的对话",
            "尚未整理的内容会暂停处理",
            "已经开始整理的内容可能仍会保存",
            "已有记忆不会删除",
            "保存期限不会延长",
            "尚未超过保存期限",
            "对话模型",
            "AI 助手中开启对话记录",
            "助手不会自动开始记录",
          ])
        expect(text).toContain(part);
      expect(
        container.querySelector('a[href="#inference"]')?.textContent,
      ).toContain(lang === "en" ? "Set up chat model" : "设置对话模型");
      expect(control()!.getAttribute("aria-describedby")).toContain(
        "session-memory-scope",
      );
    }
  });
});
