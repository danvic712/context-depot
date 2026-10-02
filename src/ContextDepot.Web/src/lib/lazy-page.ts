import type { ComponentType } from "react";
import { PageLoadError } from "@/components/feedback/PageState";

// Resolve failed imports to a page so the router can exit its hydration fallback.
export function lazyPage(load: () => Promise<{ Component: ComponentType }>) {
  return () => load().catch(() => ({ Component: PageLoadError }));
}
