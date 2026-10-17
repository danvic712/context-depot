import { flushSync } from "react-dom";

const animations = new WeakMap<HTMLElement, Animation>();

/** Commits immediately, then fades the caller-owned surface without coupling to a page selector. */
export function changeDisplay(region: HTMLElement | null, update: () => void) {
  if (typeof window === "undefined") {
    update();
    return;
  }
  const current = region ? animations.get(region) : undefined;
  const opacity =
    region && current?.playState === "running"
      ? window.getComputedStyle(region).opacity
      : "0.72";
  current?.cancel();
  if (region) animations.delete(region);
  // Selection responds immediately; only the content surface animates.
  flushSync(update);
  if (
    !region?.animate ||
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  )
    return;
  const animation = region.animate([{ opacity }, { opacity: 1 }], {
    duration: 160,
    easing: "ease-out",
  });
  animations.set(region, animation);
  void animation.finished
    .catch(() => {})
    .finally(() => {
      if (animations.get(region) === animation) animations.delete(region);
    });
}
