import { contextKinds } from "./search-api";

export function normalizeKnowledgeParams(params: URLSearchParams) {
  const next = new URLSearchParams(params);
  for (const key of ["preview", "state", "view"]) next.delete(key);
  const choices: Record<string, readonly string[]> = {
    type: ["contexts", "documents"],
    kind: contextKinds,
    read: ["1"],
  };
  for (const [key, allowed] of Object.entries(choices)) {
    const value = next.get(key);
    if (value !== null && !allowed.includes(value)) next.delete(key);
  }
  const workspace = next.get("workspace");
  if (workspace !== null) {
    if (workspace.trim()) next.set("workspace", workspace.trim());
    else next.delete("workspace");
  }
  const selected = next.get("selected");
  if (selected !== null && !/^(context|document):.+$/.test(selected))
    next.delete("selected");
  if (next.get("type") === "documents") next.delete("kind");
  else if (next.has("kind")) next.set("type", "contexts");
  return next;
}
