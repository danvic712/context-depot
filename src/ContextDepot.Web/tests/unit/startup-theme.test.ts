import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const html = readFileSync(new URL("../../index.html", import.meta.url), "utf8");
const script = html.match(/<script>([\s\S]*?)<\/script>/)?.[1];
if (!script) throw new Error("Missing startup theme script");

function startupTheme(
  values: Record<string, string>,
  systemDark = false,
  storageUnavailable = false,
) {
  const document = { documentElement: { dataset: { theme: "" }, lang: "" } };
  runInNewContext(script!, {
    document,
    localStorage: {
      getItem(key: string) {
        if (storageUnavailable) throw new Error("Storage unavailable");
        return values[key] ?? null;
      },
    },
    matchMedia: () => ({ matches: systemDark }),
  });
  return document.documentElement.dataset.theme;
}

describe("Theme before the first paint", () => {
  test("uses the cached server theme before settings finish loading", () => {
    expect(startupTheme({ "contextdepot.theme-mode": "dark" })).toBe("dark");
    expect(startupTheme({ "contextdepot.theme-mode": "light" }, true)).toBe(
      "light",
    );
  });

  test("an explicit browser preference takes precedence over the cache", () => {
    expect(
      startupTheme({
        "contextdepot.theme": "light",
        "contextdepot.theme-mode": "dark",
      }),
    ).toBe("light");
    expect(
      startupTheme({
        "contextdepot.theme": "system",
        "contextdepot.theme-mode": "dark",
      }),
    ).toBe("light");
  });

  test("system mode follows the system and invalid values fall back safely", () => {
    expect(startupTheme({ "contextdepot.theme-mode": "system" }, true)).toBe(
      "dark",
    );
    expect(startupTheme({ "contextdepot.theme-mode": "invalid" }, true)).toBe(
      "dark",
    );
    expect(startupTheme({}, true, true)).toBe("dark");
  });
});
