import { sampleSpaces } from "./sample-data";

export function normalizeKnowledgeParams(params: URLSearchParams) {
  const next = new URLSearchParams(params);
  const choices: Record<string, readonly string[]> = {
    type: ["contexts", "documents"],
    kind: ["decision", "preference"],
    workspace: sampleSpaces.map((space) => space.id),
    view: ["contexts", "documents"],
    state: ["loading", "empty", "error", "permission", "degraded"],
    preview: ["1"],
  };
  for (const [key, allowed] of Object.entries(choices)) {
    const value = next.get(key);
    if (value !== null && !allowed.includes(value)) next.delete(key);
  }
  if (next.get("type") === "documents") next.delete("kind");
  else if (next.has("kind")) next.set("type", "contexts");
  return next;
}
