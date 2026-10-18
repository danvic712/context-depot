import { createSpring } from "@/lib/spring-motion";

/** CSS probes keep viewport sizing in CSS; layout is read only when it changes. */
export function createSearchDialogMotion(
  surface: HTMLElement,
  overlay: HTMLElement | null,
  initialExpanded: boolean,
  onClosed: () => void,
) {
  const compact = surface.querySelector<HTMLElement>(
    "[data-dialog-size=compact]",
  )!;
  const expanded = surface.querySelector<HTMLElement>(
    "[data-dialog-size=expanded]",
  )!;
  let full = initialExpanded;
  let disposed = false;
  const measure = () => (full ? expanded : compact).offsetHeight;
  const height = createSpring(measure(), (value) => {
    surface.style.height = `${Math.max(0, value)}px`;
  });
  const visibility = createSpring(
    0,
    (value) => {
      const opacity = String(Math.max(0, Math.min(1, value)));
      surface.style.opacity = opacity;
      surface.style.transform = `translate3d(0, ${6 * (1 - value)}px, 0) scale(${0.99 + 0.01 * value})`;
      if (overlay) overlay.style.opacity = opacity;
    },
    0.24,
  );
  const observer = new ResizeObserver(() => {
    if (!disposed) height.to(measure());
  });
  observer.observe(compact);
  observer.observe(expanded);
  visibility.to(1);
  return {
    setExpanded(value: boolean) {
      if (disposed || value === full) return;
      full = value;
      height.to(measure());
    },
    open() {
      visibility.to(1);
    },
    close() {
      visibility.to(0, onClosed);
    },
    dispose() {
      disposed = true;
      observer.disconnect();
      height.dispose();
      visibility.dispose();
    },
  };
}
