import { useEffect, useState } from "react";
import { paginationDefaults } from "@/lib/pagination";
import type { WorkspaceSortOrder } from "./directory-enums";

const branchStorageKey = "contextdepot.workspace-tree-branches";

function readBranches(): Map<string, number> {
  try {
    const saved: unknown = JSON.parse(
      sessionStorage.getItem(branchStorageKey) ?? "[]",
    );
    if (Array.isArray(saved))
      return new Map(
        saved.filter(
          (entry): entry is [string, number] =>
            Array.isArray(entry) &&
            typeof entry[0] === "string" &&
            Number.isSafeInteger(entry[1]) &&
            entry[1] > 0 &&
            entry[1] <= paginationDefaults.maximumPage,
        ),
      );
  } catch {
    // Storage is optional for directory navigation.
  }
  return new Map();
}

export function useWorkspaceTree(
  sort: WorkspaceSortOrder,
  directoryPage: number,
) {
  const [branches, setBranches] = useState(readBranches);
  useEffect(() => {
    try {
      sessionStorage.setItem(branchStorageKey, JSON.stringify([...branches]));
    } catch {
      // Expansions still work for this visit.
    }
  }, [branches]);
  function setPages(id: string, pages: number) {
    setBranches((current) => {
      const next = new Map(current);
      if (pages > 0) next.set(id, pages);
      else next.delete(id);
      return next;
    });
  }
  return { branches, setPages, sort, directoryPage };
}
