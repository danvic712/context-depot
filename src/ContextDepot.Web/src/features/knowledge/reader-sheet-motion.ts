import { advanceSpring } from "@/lib/spring-motion";
export { advanceSpring as advanceReaderSpring } from "@/lib/spring-motion";

export function createReaderSheetMotion(
  surface: HTMLElement,
  overlay: HTMLElement | null,
  onClosed: () => void,
) {
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  let width = surface.getBoundingClientRect().width;
  let position = reduced.matches ? 0 : width;
  let velocity = 0;
  let closing = false;
  let disposed = false;
  let frame = 0;
  let previousTime = performance.now();
  function paint() {
    surface.style.transform = `translate3d(${position}px, 0, 0)`;
    if (overlay)
      overlay.style.opacity = String(
        Math.max(0, Math.min(1, 1 - position / (width || 1))),
      );
  }
  function advance(time: number) {
    const state = advanceSpring(
      position,
      velocity,
      closing ? width : 0,
      Math.max(0, time - previousTime) / 1000,
    );
    position = state.position;
    velocity = state.velocity;
    previousTime = time;
  }
  function settle() {
    position = closing ? width : 0;
    velocity = 0;
    paint();
    surface.style.willChange = "";
    if (overlay) overlay.style.willChange = "";
    if (closing) onClosed();
  }
  function tick(time: number) {
    frame = 0;
    if (disposed) return;
    advance(time);
    paint();
    if (
      Math.abs(position - (closing ? width : 0)) < 0.75 &&
      Math.abs(velocity) < 15
    )
      settle();
    else frame = requestAnimationFrame(tick);
  }
  function move(close: boolean) {
    if (disposed) return;
    // Include the time since the last frame before changing the destination.
    advance(performance.now());
    closing = close;
    cancelAnimationFrame(frame);
    frame = 0;
    if (reduced.matches) {
      settle();
      return;
    }
    surface.style.willChange = "transform";
    if (overlay) overlay.style.willChange = "opacity";
    frame = requestAnimationFrame(tick);
  }
  function changePreference() {
    if (reduced.matches) {
      cancelAnimationFrame(frame);
      frame = 0;
      settle();
    }
  }
  const resize = new ResizeObserver(() => {
    if (disposed) return;
    const nextWidth = surface.getBoundingClientRect().width;
    if (nextWidth === width || !nextWidth) return;
    const ratio = nextWidth / (width || nextWidth);
    position *= ratio;
    velocity *= ratio;
    width = nextWidth;
    paint();
  });
  resize.observe(surface);
  reduced.addEventListener("change", changePreference);
  paint();
  move(false);
  return {
    open: () => move(false),
    close: () => move(true),
    dispose() {
      disposed = true;
      cancelAnimationFrame(frame);
      resize.disconnect();
      reduced.removeEventListener("change", changePreference);
    },
  };
}
