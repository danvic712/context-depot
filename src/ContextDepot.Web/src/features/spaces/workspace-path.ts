import { pinyin } from "pinyin-pro";

export function suggestWorkspacePath(name: string): string {
  const normalized = name.trim().normalize("NFKC");
  if (!normalized) return "";
  const path = pinyin(normalized, {
    toneType: "none",
    nonZh: "consecutive",
    v: true,
  })
    .normalize("NFKD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 100)
    .replace(/-+$/g, "");
  return path || "space";
}
