import { useLayoutEffect, useRef, type RefObject } from "react";
import { createSpring } from "@/lib/spring-motion";

export function useSettingsNavigationMotion(
  ref: RefObject<HTMLElement | null>,
  active: string,
) {
  const update = useRef<(() => void) | undefined>(undefined);
  useLayoutEffect(() => {
    const nav = ref.current;
    if (!nav) return;
    let springs: Record<string, ReturnType<typeof createSpring>> | undefined;
    const measure = () => {
      const link = nav.querySelector<HTMLElement>("[aria-current]");
      if (!link) return;
      const targets = {
        x: link.offsetLeft,
        y: link.offsetTop,
        width: link.offsetWidth / 100,
        height: link.offsetHeight / 44,
      };
      if (!springs) {
        springs = Object.fromEntries(
          Object.entries(targets).map(([key, value]) => [
            key,
            createSpring(value, (current) => {
              nav.style.setProperty(
                `--nav-indicator-${key}`,
                key === "x" || key === "y" ? `${current}px` : String(current),
              );
            }),
          ]),
        );
        nav.dataset.motionReady = "true";
      } else {
        for (const [key, value] of Object.entries(targets))
          springs[key].to(value);
      }
    };
    update.current = measure;
    const observer = new ResizeObserver(measure);
    observer.observe(nav);
    for (const link of nav.querySelectorAll("a")) observer.observe(link);
    measure();
    return () => {
      observer.disconnect();
      for (const spring of Object.values(springs ?? {})) spring.dispose();
      delete nav.dataset.motionReady;
      update.current = undefined;
    };
  }, [ref]);
  useLayoutEffect(() => update.current?.(), [active]);
}
