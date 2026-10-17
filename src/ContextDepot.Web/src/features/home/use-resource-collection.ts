import { useKnowledgeReading } from "@/features/knowledge/knowledge-reading-context";
import { useCallback, useState } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import type { ResourceCollection } from "./home-api";

export function useResourceCollection<T>(
  load: (signal: AbortSignal) => Promise<ResourceCollection<T>>,
) {
  const revision = useKnowledgeReading()?.revision ?? 0;
  const [attempt, setAttempt] = useState(0);
  const resource = useRequestResource("collection", attempt + revision, load);
  const refresh = useCallback(() => setAttempt((value) => value + 1), []);
  return { ...resource, refresh };
}
