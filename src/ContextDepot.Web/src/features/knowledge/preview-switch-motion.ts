import { createSpring } from "@/lib/spring-motion";

export function createPreviewSwitchMotion(
  node: HTMLElement,
  status?: HTMLElement,
) {
  let shown: string | undefined;
  const spring = createSpring(
    1,
    (value) => {
      node.style.opacity = String(0.2 + 0.8 * value);
    },
    0.24,
  );
  const loadingSpring = status
    ? createSpring(
        0,
        (value) => {
          status.style.opacity = String(value);
        },
        0.22,
      )
    : undefined;
  return {
    update(pending: boolean, identity?: string, loading = pending) {
      if (identity && identity !== shown) {
        shown = identity;
        if (node.parentElement) node.parentElement.scrollTop = 0;
      }
      spring.to(pending ? 0 : 1);
      loadingSpring?.to(loading ? 1 : 0);
    },
    dispose() {
      spring.dispose();
      loadingSpring?.dispose();
    },
  };
}
