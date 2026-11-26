import { useState } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import type { SpacePageSeed } from "./space-route-loader";

/** Use the route's first response immediately and fetch again only for local changes or retry. */
export function useSpacePageResource<T>(
  key: string,
  load: (signal: AbortSignal) => Promise<T>,
  seed?: SpacePageSeed<T>,
) {
  const [retry, setRetry] = useState<{
    seed?: SpacePageSeed<T>;
    attempt: number;
  }>();
  const ready = seed && seed !== retry?.seed;
  const response = useRequestResource(
    key,
    retry?.attempt ?? 0,
    ready ? null : load,
  );
  return {
    ...(ready ? seed.resource : response),
    retry: () =>
      setRetry((previous) => ({ seed, attempt: (previous?.attempt ?? 0) + 1 })),
  };
}
