import { useCallback, useSyncExternalStore } from "react";
import { paginationDefaults, readPageSize } from "@/lib/pagination";

interface DisplayPreferences {
  directoryPageSize: number;
  knowledgePageSize: number;
  archivePageSize: number;
  knowledgeLayout: "list" | "cards";
}
const defaults: DisplayPreferences = {
  directoryPageSize: paginationDefaults.pageSize,
  knowledgePageSize: paginationDefaults.pageSize,
  archivePageSize: paginationDefaults.pageSize,
  knowledgeLayout: "list",
};
const sessionValues = new Map<string, string>();
const listeners = new Set<() => void>();
const storageKey = (name: keyof DisplayPreferences) =>
  `contextdepot.display.${name}`;

export function readDisplayPreference<K extends keyof DisplayPreferences>(
  name: K,
): DisplayPreferences[K] {
  let value: string | null = null;
  if (sessionValues.has(storageKey(name)))
    value = sessionValues.get(storageKey(name))!;
  else
    try {
      value = localStorage.getItem(storageKey(name));
    } catch {
      value = sessionValues.get(storageKey(name)) ?? null;
    }
  if (name === "knowledgeLayout")
    return (value === "cards" ? "cards" : "list") as DisplayPreferences[K];
  return readPageSize(value) as DisplayPreferences[K];
}

export function saveDisplayPreference<K extends keyof DisplayPreferences>(
  name: K,
  value: DisplayPreferences[K],
) {
  const key = storageKey(name);
  try {
    localStorage.setItem(key, String(value));
    sessionValues.delete(key);
  } catch {
    sessionValues.set(key, String(value));
  }
  listeners.forEach((notify) => notify());
}
function subscribe(notify: () => void) {
  listeners.add(notify);
  window.addEventListener("storage", notify);
  return () => {
    listeners.delete(notify);
    window.removeEventListener("storage", notify);
  };
}
export function useDisplayPreference<K extends keyof DisplayPreferences>(
  name: K,
) {
  const value = useSyncExternalStore(
    subscribe,
    useCallback(() => readDisplayPreference(name), [name]),
    () => defaults[name],
  );
  const setValue = useCallback(
    (next: DisplayPreferences[K]) => saveDisplayPreference(name, next),
    [name],
  );
  return [value, setValue] as const;
}
