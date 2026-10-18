import { createSpring } from "@/lib/spring-motion";

/** Measure only on resize or a new target, never on every animation frame. */
export function createFilterDisclosureMotion(
  node: HTMLDivElement,
  initialOpen: boolean,
) {
  let open = initialOpen;
  let moving = false;
  let disposed = false;
  node.hidden = false;
  const content = node.firstElementChild;
  const measure = () => content?.getBoundingClientRect().height ?? 0;
  let height = createSpring(open ? measure() : 0, (value) => {
    node.style.height = `${Math.max(0, value)}px`;
  });
  const opacity = createSpring(open ? 1 : 0, (value) => {
    node.style.opacity = String(Math.max(0, Math.min(1, value)));
  });
  function rested() {
    moving = false;
    if (open) node.style.height = "auto";
    else node.hidden = true;
  }
  rested();
  const observer = new ResizeObserver(() => {
    if (!disposed && moving && open) height.to(measure(), rested);
  });
  if (content) observer.observe(content);
  return {
    setOpen(next: boolean) {
      if (disposed || next === open) return;
      if (!moving) {
        // An expanded panel uses auto height; its contents may have changed
        // since the spring last settled. Begin from its actual visible size.
        const current = node.hidden ? 0 : node.getBoundingClientRect().height;
        height.dispose();
        height = createSpring(current, (value) => {
          node.style.height = `${Math.max(0, value)}px`;
        });
      }
      open = next;
      node.hidden = false;
      moving = true;
      height.to(open ? measure() : 0, rested);
      opacity.to(open ? 1 : 0);
    },
    dispose() {
      disposed = true;
      observer.disconnect();
      height.dispose();
      opacity.dispose();
    },
  };
}
