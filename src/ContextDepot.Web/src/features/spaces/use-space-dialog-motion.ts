import { useCallback, useLayoutEffect, useRef } from "react";
import { createSpring } from "@/lib/spring-motion";

export function useSpaceDialogMotion(onClosed: () => void) {
  const overlay = useRef<HTMLDivElement>(null);
  const spring = useRef<ReturnType<typeof createSpring> | null>(null);
  const closed = useRef(onClosed);
  useLayoutEffect(() => {
    closed.current = onClosed;
  }, [onClosed]);
  const ref = useCallback((node: HTMLDivElement | null) => {
    spring.current?.dispose();
    spring.current = null;
    if (!node) return;
    spring.current = createSpring(0, (value) => {
      node.style.transform = `translateY(${8 * (1 - value)}px) scale(${0.97 + 0.03 * value})`;
      node.style.opacity = String(Math.max(0, Math.min(1, value)));
      if (overlay.current) overlay.current.style.opacity = node.style.opacity;
    });
    spring.current.to(1);
  }, []);
  const overlayRef = useCallback((node: HTMLDivElement | null) => {
    overlay.current = node;
  }, []);
  return {
    ref,
    overlayRef,
    close() {
      if (spring.current) spring.current.to(0, () => closed.current());
      else closed.current();
    },
    open() {
      spring.current?.to(1);
    },
  };
}
