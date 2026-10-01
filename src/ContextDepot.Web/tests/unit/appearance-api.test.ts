import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { initializeI18n, type Messages } from "../../src/lib/i18n";
import english from "../../../../locales/en-US/application-settings.json";
import { httpClient } from "../../src/lib/http-client";
import {
  getAppearance,
  setAppearanceTheme,
  setAppearanceLanguage,
  toUiLanguage,
} from "../../src/features/settings/appearance-api";

let settings = { theme: "system", language: "zh-CN" };
let failure = false;
let invalidResponse = false;
const server = Bun.serve({
  port: 0,
  async fetch(request) {
    if (failure)
      return Response.json({ code: "AppearanceUnavailable" }, { status: 503 });
    if (invalidResponse)
      return Response.json({ theme: "invalid", language: 12 });
    const path = new URL(request.url).pathname;
    const body: unknown =
      request.method === "PUT" ? await request.json() : null;
    if (path.endsWith("/theme")) {
      if (
        !body ||
        typeof body !== "object" ||
        !("theme" in body) ||
        typeof body.theme !== "string"
      )
        return new Response(null, { status: 400 });
      settings = { ...settings, theme: body.theme };
      return Response.json({ theme: settings.theme });
    }
    if (path.endsWith("/language")) {
      if (
        !body ||
        typeof body !== "object" ||
        !("language" in body) ||
        typeof body.language !== "string"
      )
        return new Response(null, { status: 400 });
      settings = { ...settings, language: body.language };
      return Response.json({ language: settings.language });
    }
    return Response.json(settings);
  },
});
const baseURL = httpClient.defaults.baseURL;
httpClient.defaults.baseURL = `${server.url}api`;
beforeAll(() => initializeI18n("en", async () => english as Messages));
afterAll(async () => {
  settings = { theme: "system", language: "en-US" };
  await getAppearance();
  server.stop(true);
  httpClient.defaults.baseURL = baseURL;
});

describe("Appearance API over axios", () => {
  test("reads defaults and maps backend cultures to UI languages", async () => {
    expect(await getAppearance()).toEqual({
      theme: "system",
      language: "zh-CN",
    });
    expect(toUiLanguage("zh-CN")).toBe("zh");
    expect(toUiLanguage("en-US")).toBe("en");
  });
  test("field updates preserve the other preference and survive a fresh GET", async () => {
    expect(await setAppearanceTheme("dark")).toBe("dark");
    expect(await getAppearance()).toEqual({ theme: "dark", language: "zh-CN" });
    expect(await setAppearanceLanguage("en")).toBe("en");
    expect(await getAppearance()).toEqual({ theme: "dark", language: "en-US" });
  });
  test("a failed save rejects and leaves stored settings intact", async () => {
    failure = true;
    try {
      await expect(setAppearanceTheme("light")).rejects.toThrow();
    } finally {
      failure = false;
    }
    expect(await getAppearance()).toEqual({ theme: "dark", language: "en-US" });
  });
  test("invalid responses are rejected instead of applied to the UI", async () => {
    invalidResponse = true;
    try {
      await expect(getAppearance()).rejects.toThrow(
        "Invalid appearance response",
      );
    } finally {
      invalidResponse = false;
    }
  });
  test("an aborted request does not replace defaults", async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(getAppearance(controller.signal)).rejects.toThrow();
  });
});
