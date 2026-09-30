import { lazy, Suspense } from "react";
import { Skeleton } from "@/components/ui/skeleton";

const MarkdownBody = lazy(() =>
  import("./MarkdownBody").then((module) => ({ default: module.MarkdownBody })),
);

export function MarkdownReader({ content }: { content: string }) {
  return (
    <Suspense fallback={<Skeleton className="h-40 w-full" />}>
      <MarkdownBody content={content} />
    </Suspense>
  );
}
