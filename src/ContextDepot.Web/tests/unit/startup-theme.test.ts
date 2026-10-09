import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const html = readFileSync(new URL("../../index.html", import.meta.url), "utf8");
const script = html.match(/<script>([\s\S]*?)<\/script>/)?.[1];
if (!script) throw new Error("Missing startup theme script");

function startupAppearance(
  values: Record<string, string>,
  {
    systemDark = false,
    storageUnavailable = false,
    pathname = "/",
    languages = ["en-US"],
  }: {
    systemDark?: boolean;
    storageUnavailable?: boolean;
    pathname?: string;
    languages?: string[];
  } = {},
) {
  const document = { documentElement: { dataset: { theme: "" }, lang: "" } };
  runInNewContext(script!, {
    document,
    location: { pathname },
    navigator: { languages, language: languages[0] ?? "en-US" },
    localStorage: {
      getItem(key: string) {
        if (storageUnavailable) throw new Error("Storage unavailable");
        return values[key] ?? null;
      },
    },
    matchMedia: () => ({ matches: systemDark }),
  });
  return document.documentElement;
}

describe("Theme before the first paint", () => {
  test("uses the cached server theme before settings finish loading", () => {
    expect(
      startupAppearance({ "contextdepot.theme-mode": "dark" }).dataset.theme,
    ).toBe("dark");
    expect(
      startupAppearance(
        { "contextdepot.theme-mode": "light" },
        { systemDark: true },
      ).dataset.theme,
    ).toBe("light");
  });

  test("an explicit browser preference takes precedence over the cache", () => {
    expect(
      startupAppearance({
        "contextdepot.theme": "light",
        "contextdepot.theme-mode": "dark",
      }).dataset.theme,
    ).toBe("light");
    expect(
      startupAppearance({
        "contextdepot.theme": "system",
        "contextdepot.theme-mode": "dark",
      }).dataset.theme,
    ).toBe("light");
  });

  test("system mode follows the system and invalid values fall back safely", () => {
    expect(
      startupAppearance(
        { "contextdepot.theme-mode": "system" },
        { systemDark: true },
      ).dataset.theme,
    ).toBe("dark");
    expect(
      startupAppearance(
        { "contextdepot.theme-mode": "invalid" },
        { systemDark: true },
      ).dataset.theme,
    ).toBe("dark");
    expect(
      startupAppearance({}, { systemDark: true, storageUnavailable: true })
        .dataset.theme,
    ).toBe("dark");
  });

  test("setup starts with system theme even when regular pages cached a different preference", () => {
    const appearance = startupAppearance(
      {
        "contextdepot.theme": "dark",
        "contextdepot.theme-mode": "dark",
        "contextdepot.language": "en",
      },
      { pathname: "/setup", languages: ["zh-CN"] },
    );
    expect(appearance.dataset.theme).toBe("light");
    expect(appearance.lang).toBe("zh-CN");
  });

  test("a manual setup selection applies before the first paint", () => {
    const appearance = startupAppearance(
      {
        "contextdepot.setup.theme": "light",
        "contextdepot.setup.language": "en",
      },
      { pathname: "/setup", systemDark: true, languages: ["zh-CN"] },
    );
    expect(appearance.dataset.theme).toBe("light");
    expect(appearance.lang).toBe("en");
  });

  test("language follows browser preferences and still works when storage is blocked", () => {
    expect(startupAppearance({}, { languages: ["fr-FR", "zh-TW"] }).lang).toBe(
      "zh-CN",
    );
    expect(startupAppearance({}, { languages: ["de-DE"] }).lang).toBe("en");
    expect(
      startupAppearance({}, { languages: ["zh-HK"], storageUnavailable: true })
        .lang,
    ).toBe("zh-CN");
    expect(
      startupAppearance(
        { "contextdepot.language": "en" },
        { languages: ["zh-CN"] },
      ).lang,
    ).toBe("en");
  });
});
