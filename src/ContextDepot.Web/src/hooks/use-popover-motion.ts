import { useCallback, useRef, useState } from "react";
import { createSpring } from "@/lib/spring-motion";

/** Keep the surface present through dismissal so a new press can reverse it. */
export function usePopoverMotion() {
  const [open, setOpen] = useState(false);
  const [present, setPresent] = useState(false);
  const target = useRef(false);
  const spring = useRef<ReturnType<typeof createSpring> | null>(null);
  const ref = useCallback((node: HTMLDivElement | null) => {
    spring.current?.dispose();
    spring.current = null;
    if (!node) return;
    spring.current = createSpring(0, (value) => {
      const side = node.dataset.side;
      const distance = 6 * (1 - value);
      const x = side === "left" ? distance : side === "right" ? -distance : 0;
      const y = side === "top" ? distance : side === "bottom" ? -distance : 0;
      node.style.transform = `translate3d(${x}px, ${y}px, 0) scale(${0.98 + 0.02 * value})`;
      node.style.opacity = String(Math.max(0, Math.min(1, value)));
    });
    spring.current.to(target.current ? 1 : 0);
  }, []);

  function onOpenChange(next: boolean) {
    target.current = next;
    if (next) setPresent(true);
    else if (!spring.current) setPresent(false);
    setOpen(next);
    spring.current?.to(
      next ? 1 : 0,
      next ? undefined : () => setPresent(false),
    );
  }

  return { open, present, onOpenChange, ref };
}
