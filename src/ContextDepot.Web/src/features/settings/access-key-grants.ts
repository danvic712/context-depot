import type { KeyWorkspace } from "./settings-api";

export function filterKeyWorkspaces(
  workspaces: KeyWorkspace[],
  query: string,
  onlySelected: boolean,
  grants: string[],
) {
  const term = query.trim().toLowerCase();
  const selected = new Set(grants);
  return workspaces.filter(
    (space) =>
      (!onlySelected || selected.has(space.id)) &&
      (!term ||
        space.name.toLowerCase().includes(term) ||
        space.path.toLowerCase().includes(term)),
  );
}

export function setVisibleKeyGrants(
  grants: string[],
  visible: KeyWorkspace[],
  selected: boolean,
) {
  const visibleIds = new Set(visible.map((space) => space.id));
  return selected
    ? [...new Set([...grants, ...visibleIds])]
    : grants.filter((id) => !visibleIds.has(id));
}
