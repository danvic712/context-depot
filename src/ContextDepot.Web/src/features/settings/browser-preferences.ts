export type Theme = "system" | "light" | "dark";

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
