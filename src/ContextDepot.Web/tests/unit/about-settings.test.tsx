import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { AboutSettings } from "../../src/features/settings/AboutSettings";
import { initializeI18n } from "../../src/lib/i18n";

async function language(lang: "en" | "zh") {
  await initializeI18n(lang, async (requested) => {
    const locale = requested === "zh" ? "zh-CN" : "en-US";
    const messages = await Promise.all(
      ["application-settings", "ui-states", "navigation-and-actions"].map(
        (file) =>
          Bun.file(
            new URL(
              `../../../../locales/${locale}/${file}.json`,
              import.meta.url,
            ),
          ).json(),
      ),
    );
    return Object.assign({}, ...messages);
  });
}

describe("About settings", () => {
  test("shows the installed version once, with a localized copy action and the real library name", async () => {
    for (const lang of ["en", "zh"] as const) {
      await language(lang);
      const html = renderToStaticMarkup(
        <AboutSettings
          version="1.2.3-rc.1"
          versionPending={false}
          versionFailed={false}
          depotName="Research notes"
          depotPending={false}
        />,
      );
      expect(html.match(/1\.2\.3-rc\.1/g)?.length).toBe(1);
      expect(html).toContain("Research notes");
      expect(html).toContain(lang === "zh" ? "复制版本号" : "Copy version");
      expect(html).toContain(
        lang === "zh" ? "在 GitHub 上查看" : "View on GitHub",
      );
    }
  });

  test("failed metadata cannot expose or copy an outdated version", async () => {
    await language("en");
    const html = renderToStaticMarkup(
      <AboutSettings
        version="1.2.3-rc.1"
        versionPending={false}
        versionFailed
        depotName={undefined}
        depotPending={false}
      />,
    );
    expect(html).toContain("Could not load");
    expect(html).toContain("Temporarily unavailable");
    expect(html).not.toContain("1.2.3-rc.1");
    expect(html).not.toContain("<button");
  });

  test("loading and missing metadata keep honest placeholders without a copy action", async () => {
    await language("en");
    for (const pending of [true, false]) {
      const html = renderToStaticMarkup(
        <AboutSettings
          version={null}
          versionPending={pending}
          versionFailed={false}
          depotName={undefined}
          depotPending={pending}
        />,
      );
      expect(html).toContain(pending ? "Loading" : "Not available");
      expect(html).not.toContain("0.0.0");
      expect(html).not.toContain("<button");
    }
  });
});
