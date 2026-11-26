import { useCallback, useLayoutEffect, useRef } from "react";
import { createDisclosureMotion } from "@/lib/disclosure-motion";

/** Keep the disclosure mounted until its closing spring settles. */
export function useDisclosureMotion(open: boolean, onClosed?: () => void) {
  const motion = useRef<ReturnType<typeof createDisclosureMotion> | null>(null);
  const target = useRef(open);
  const closed = useRef(onClosed);
  useLayoutEffect(() => {
    closed.current = onClosed;
  }, [onClosed]);
  useLayoutEffect(() => {
    target.current = open;
    motion.current?.setOpen(open);
  }, [open]);

  return useCallback((node: HTMLDivElement | null) => {
    motion.current?.dispose();
    motion.current = null;
    if (!node) return;
    motion.current = createDisclosureMotion(node, target.current, () =>
      closed.current?.(),
    );
    return () => {
      motion.current?.dispose();
      motion.current = null;
    };
  }, []);
}
