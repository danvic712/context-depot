import { useCallback, useState } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import type { ResourceCollection } from "./home-api";

export function useResourceCollection<T>(
  load: (signal: AbortSignal) => Promise<ResourceCollection<T>>,
) {
  const [attempt, setAttempt] = useState(0);
  const resource = useRequestResource("collection", attempt, load);
  const refresh = useCallback(() => setAttempt((value) => value + 1), []);
  return { ...resource, refresh };
}
