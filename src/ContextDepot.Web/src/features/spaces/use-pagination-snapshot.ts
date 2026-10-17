import { useState } from "react";
import type { Resource } from "@/hooks/use-request-resource";

/** Preserve only counts while another page loads; never reuse another page's rows. */
export function usePaginationSnapshot<
  T extends { totalCount: number; pageSize: number },
>(scope: string, resource: Resource<T>) {
  const [snapshot, setSnapshot] = useState<{
    scope: string;
    totalCount: number;
    pageSize: number;
  }>();
  const accessible = !resource.error || resource.error.retryable;
  const data = accessible ? resource.data : undefined;
  if (
    data &&
    (snapshot?.scope !== scope ||
      snapshot.totalCount !== data.totalCount ||
      snapshot.pageSize !== data.pageSize)
  )
    setSnapshot({
      scope,
      totalCount: data.totalCount,
      pageSize: data.pageSize,
    });
  return (
    data ??
    (accessible && resource.pending && snapshot?.scope === scope
      ? snapshot
      : undefined)
  );
}
