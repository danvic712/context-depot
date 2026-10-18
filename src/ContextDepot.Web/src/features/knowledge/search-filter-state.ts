import type { SearchHit, SearchWorkspace } from "./search-api";

export function activeSearchFilters(params: URLSearchParams) {
  return ["type", "kind", "workspace"].filter((key) => {
    const value = params.get(key);
    return value !== null && (key === "workspace" || value !== "all");
  }).length;
}

export function filterSearchHits(items: SearchHit[], type: string | null) {
  if (!type || type === "all") return items;
  if (type !== "contexts" && type !== "documents") return [];
  return items.filter(
    (hit) => hit.type === (type === "contexts" ? "context" : "document"),
  );
}

export function matchingSearchSpaces(spaces: SearchWorkspace[], query: string) {
  const term = query.trim().toLocaleLowerCase();
  return spaces.filter((space) =>
    space.name.toLocaleLowerCase().includes(term),
  );
}
