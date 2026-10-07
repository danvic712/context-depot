import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { ApplicationVersion } from "../../src/components/meta/ApplicationVersion";
import { initializeI18n } from "../../src/lib/i18n";

async function language(lang: "en" | "zh") {
  await initializeI18n(lang, async (requested) =>
    Bun.file(
      new URL(
        `../../../../locales/${requested === "zh" ? "zh-CN" : "en-US"}/ui-states.json`,
        import.meta.url,
      ),
    ).json(),
  );
}
function render(
  version: string | null | undefined,
  pending = false,
  failed = false,
) {
  return renderToStaticMarkup(
    <ApplicationVersion version={version} pending={pending} failed={failed} />,
  );
}
describe("Application version", () => {
  test("displays the backend version once with its full prerelease suffix", async () => {
    for (const lang of ["en", "zh"] as const) {
      await language(lang);
      const html = render("1.2.3-rc.1");
      expect(html).toContain("1.2.3-rc.1");
      expect(html.match(/1\.2\.3-rc\.1/g)?.length).toBe(1);
      expect(html).toContain(lang === "zh" ? "版本" : "Version");
      expect(html).not.toContain("<button");
    }
  });
  test("loading, errors and missing metadata never invent a backend version", async () => {
    await language("en");
    expect(render(undefined, true)).toContain("Loading");
    const failure = render("1.2.3-rc.1", false, true);
    expect(failure).toContain("Temporarily unavailable");
    expect(failure).not.toContain("1.2.3-rc.1");
    expect(render(null)).toContain("Not provided by server");
  });
});
