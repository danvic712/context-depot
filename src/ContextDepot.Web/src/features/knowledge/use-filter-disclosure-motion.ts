import { useCallback, useLayoutEffect, useRef } from "react";
import { createFilterDisclosureMotion } from "./filter-disclosure-motion";

/** Keep Radix's disclosure reversible, including while it is still settling. */
export function useFilterDisclosureMotion(open: boolean) {
  const motion = useRef<ReturnType<typeof createFilterDisclosureMotion> | null>(
    null,
  );
  const target = useRef(open);
  useLayoutEffect(() => {
    target.current = open;
    motion.current?.setOpen(open);
  }, [open]);

  return useCallback((node: HTMLDivElement | null) => {
    motion.current?.dispose();
    motion.current = null;
    if (!node) return;
    motion.current = createFilterDisclosureMotion(node, target.current);
    return () => {
      motion.current?.dispose();
      motion.current = null;
    };
  }, []);
}
