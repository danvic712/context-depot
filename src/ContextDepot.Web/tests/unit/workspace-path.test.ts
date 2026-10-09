import { describe, expect, test } from "bun:test";
import { suggestWorkspacePath } from "../../src/features/spaces/workspace-path";

describe("Workspace path suggestions", () => {
  test.each([
    ["Research Notes", "research-notes"],
    ["研究笔记", "yan-jiu-bi-ji"],
    ["重庆项目", "chong-qing-xiang-mu"],
    ["Vue 3 学习", "vue-3-xue-xi"],
    ["绿色旅行", "lv-se-lv-xing"],
    [" Café & Résumé / 2026 ", "cafe-resume-2026"],
    ["ＡＢＣ１２３", "abc123"],
    ["--- Notes ___ 2026 ---", "notes-2026"],
  ])("suggests a readable identifier for %s", (name, expected) => {
    expect(suggestWorkspacePath(name)).toBe(expected);
  });

  test("empty names stay empty and names without Latin or Chinese text receive a valid fallback", () => {
    expect(suggestWorkspacePath(" \t ")).toBe("");
    expect(suggestWorkspacePath("🧪")).toBe("space");
  });

  test("long suggestions obey the API limit without ending in a hyphen", () => {
    for (const name of ["研究".repeat(100), "a".repeat(99) + " b"])
      expect(suggestWorkspacePath(name)).toMatch(
        /^(?=.{1,100}$)[a-z0-9]+(?:-[a-z0-9]+)*$/,
      );
  });
});
