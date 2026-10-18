import { useLayoutEffect, useRef, type RefObject } from "react";
import { createSpring } from "@/lib/spring-motion";

export function useNavigationIndicatorMotion(
  ref: RefObject<HTMLElement | null>,
  active: string | undefined,
) {
  const update = useRef<((value: string | undefined) => void) | undefined>(
    undefined,
  );
  useLayoutEffect(() => {
    const nav = ref.current;
    if (!nav) return;
    let destination: string | undefined;
    let springs: Record<string, ReturnType<typeof createSpring>> | undefined;
    const measure = (snap = false) => {
      const link = [
        ...nav.querySelectorAll<HTMLElement>("[data-navigation-item]"),
      ].find((item) => item.dataset.navigationItem === destination);
      if (!link) {
        nav.dataset.indicatorVisible = "false";
        return;
      }
      nav.dataset.indicatorVisible = "true";
      const targets = {
        x: link.offsetLeft,
        y: link.offsetTop,
        width: link.offsetWidth / 100,
        height: link.offsetHeight / 44,
      };
      if (snap) {
        for (const spring of Object.values(springs ?? {})) spring.dispose();
        springs = undefined;
      }
      if (!springs) {
        springs = Object.fromEntries(
          Object.entries(targets).map(([key, value]) => [
            key,
            createSpring(
              value,
              (current) => {
                nav.style.setProperty(
                  `--nav-indicator-${key}`,
                  key === "x" || key === "y" ? `${current}px` : String(current),
                );
              },
              0.24,
            ),
          ]),
        );
        nav.dataset.motionReady = "true";
      } else {
        for (const [key, value] of Object.entries(targets))
          springs[key].to(value);
      }
    };
    update.current = (value) => {
      destination = value;
      measure();
    };
    const observer = new ResizeObserver(() => measure(true));
    observer.observe(nav);
    for (const link of nav.querySelectorAll("a")) observer.observe(link);
    return () => {
      observer.disconnect();
      for (const spring of Object.values(springs ?? {})) spring.dispose();
      delete nav.dataset.motionReady;
      delete nav.dataset.indicatorVisible;
      update.current = undefined;
    };
  }, [ref]);
  useLayoutEffect(() => update.current?.(active), [active]);
}
