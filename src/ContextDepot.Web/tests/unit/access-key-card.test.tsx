import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { AccessKeyCard } from "../../src/features/settings/AccessKeyCard";
import { initializeI18n } from "../../src/lib/i18n";
import type { AccessKey } from "../../src/features/settings/settings-api";

const key: AccessKey = {
  id: "key-id",
  name: "Desktop client",
  prefix: "cdk_example",
  createdAt: "2026-10-01T08:00:00Z",
  lastUsedAt: null,
  revokedAt: null,
  workspaceIds: ["granted-space", "missing-space"],
  canCreateRootWorkspaces: false,
  accessVersion: "1",
};

async function language(lang: "en" | "zh") {
  await initializeI18n(lang, async (requested) => {
    const locale = requested === "zh" ? "zh-CN" : "en-US";
    return Bun.file(
      new URL(
        `../../../../locales/${locale}/application-settings.json`,
        import.meta.url,
      ),
    ).json();
  });
}

describe("Access key display", () => {
  test("identifies a key by its masked prefix, names its grants and never offers to copy a partial key", async () => {
    for (const lang of ["en", "zh"] as const) {
      await language(lang);
      const html = renderToStaticMarkup(
        <AccessKeyCard
          accessKey={key}
          workspaces={[
            { id: "granted-space", name: "Research", path: "research" },
          ]}
          disabled={false}
          onAction={() => {}}
        />,
      );
      expect(html).toContain("cdk_example");
      expect(html).toContain(".••••");
      expect(html).toContain("Research");
      expect(html).not.toContain("granted-space");
      expect(html).not.toContain("missing-space");
      expect(html).toContain(
        lang === "zh" ? "空间已不可用" : "Space unavailable",
      );
      expect(html).toContain(lang === "zh" ? "尚未使用" : "Never used");
      expect(html).not.toContain(
        lang === "zh" ? "复制访问密钥" : "Copy access key",
      );
    }
  });

  test("disabled keys show when access ended and cannot be changed or re-enabled", async () => {
    await language("en");
    const html = renderToStaticMarkup(
      <AccessKeyCard
        accessKey={{
          ...key,
          workspaceIds: [],
          revokedAt: "2026-10-18T08:00:00Z",
        }}
        workspaces={[]}
        disabled={false}
        onAction={() => {}}
      />,
    );
    expect(html).toContain('dateTime="2026-10-18T08:00:00Z"');
    expect(html).toContain("No existing spaces granted");
    expect(html).toContain("Disabled");
    expect(html).not.toContain("<button");
  });
});
