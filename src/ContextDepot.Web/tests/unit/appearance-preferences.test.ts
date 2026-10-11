import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import {
  rememberSetupAppearance,
  resolveAppearancePreferences,
  setupAppearanceKeys,
} from "../../src/features/settings/appearance-preferences";
import { browserLanguage } from "../../src/features/settings/browser-preferences";

const storageDescriptor = Object.getOwnPropertyDescriptor(
  globalThis,
  "localStorage",
);
const navigatorDescriptor = Object.getOwnPropertyDescriptor(
  globalThis,
  "navigator",
);
let values: Map<string, string>;
beforeEach(() => {
  values = new Map();
  Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    value: {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
      removeItem: (key: string) => values.delete(key),
    },
  });
  Object.defineProperty(globalThis, "navigator", {
    configurable: true,
    value: { languages: ["zh-TW", "en-US"], language: "zh-TW" },
  });
});
afterEach(() => {
  if (storageDescriptor)
    Object.defineProperty(globalThis, "localStorage", storageDescriptor);
  else Reflect.deleteProperty(globalThis, "localStorage");
  if (navigatorDescriptor)
    Object.defineProperty(globalThis, "navigator", navigatorDescriptor);
  else Reflect.deleteProperty(globalThis, "navigator");
});

describe("Browser language preferences", () => {
  test.each([
    [["zh-CN"], "zh"],
    [["ZH-hant-TW", "en-US"], "zh"],
    [["en-GB", "zh-CN"], "en"],
    [["fr-FR", "zh-HK", "en-US"], "zh"],
    [["de-DE", "fr-FR"], "en"],
    [[], "en"],
  ] as const)(
    "matches the first supported browser language in %j",
    (languages, expected) => {
      expect(browserLanguage(languages)).toBe(expected);
    },
  );

  test("uses navigator.language when the preferred language list is empty", () => {
    Object.defineProperty(globalThis, "navigator", {
      configurable: true,
      value: { languages: [], language: "zh-CN" },
    });
    expect(browserLanguage()).toBe("zh");
  });
});

describe("Initial setup appearance", () => {
  const server = { theme: "dark", language: "en-US" } as const;

  test("uses system theme and browser language without writing defaults or reusing regular appearance caches", () => {
    values.set("contextdepot.theme", "dark");
    values.set("contextdepot.language", "en");
    values.set("contextdepot.theme-mode", "dark");
    const before = new Map(values);
    expect(resolveAppearancePreferences(server, true)).toEqual({
      theme: "system",
      language: "zh",
    });
    expect(values).toEqual(before);
  });

  test("remembers manual setup selections independently", () => {
    values.set(setupAppearanceKeys.theme, "light");
    expect(resolveAppearancePreferences(server, true)).toEqual({
      theme: "light",
      language: "zh",
    });
    values.set(setupAppearanceKeys.language, "en");
    expect(resolveAppearancePreferences(server, true)).toEqual({
      theme: "light",
      language: "en",
    });
  });

  test("keeps session selections when storage is unavailable", () => {
    Object.defineProperty(globalThis, "localStorage", {
      configurable: true,
      value: {
        getItem() {
          throw new Error("Storage unavailable");
        },
      },
    });
    expect(
      resolveAppearancePreferences(server, true, {
        theme: "light",
        language: "en",
      }),
    ).toEqual({ theme: "light", language: "en" });
  });

  test("preserves the active appearance after completing setup", () => {
    values.set(setupAppearanceKeys.theme, "light");
    values.set(setupAppearanceKeys.language, "en");
    rememberSetupAppearance({ theme: "system", language: "zh" });
    expect(resolveAppearancePreferences(server, false)).toEqual({
      theme: "system",
      language: "zh",
    });
    expect(values.has(setupAppearanceKeys.theme)).toBe(false);
    expect(values.has(setupAppearanceKeys.language)).toBe(false);
  });

  test("completed installations keep their server settings and explicit browser preferences", () => {
    values.set(setupAppearanceKeys.theme, "light");
    expect(resolveAppearancePreferences(server, false)).toEqual({
      theme: "dark",
      language: "en",
    });
    values.set("contextdepot.theme", "light");
    values.set("contextdepot.language", "zh");
    expect(resolveAppearancePreferences(server, false)).toEqual({
      theme: "light",
      language: "zh",
    });
  });
});
