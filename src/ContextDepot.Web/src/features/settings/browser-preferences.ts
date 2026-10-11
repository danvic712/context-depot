export type Theme = "system" | "light" | "dark";

export function browserLanguage(
  languages: readonly string[] = typeof navigator === "undefined"
    ? []
    : navigator.languages?.length
      ? navigator.languages
      : [navigator.language],
): "en" | "zh" {
  for (const language of languages) {
    const code = language?.toLowerCase().split("-")[0];
    if (code === "en" || code === "zh") return code;
  }
  return "en";
}

export function load<T extends string>(
  key: string,
  allowed: readonly T[],
): T | null {
  try {
    const value = localStorage.getItem(key);
    return allowed.find((x) => x === value) ?? null;
  } catch {
    return null;
  }
}
export function save(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    /* session preference still works */
  }
}
