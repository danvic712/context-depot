import { useCallback, useLayoutEffect, useRef } from "react";
import { createSearchDialogMotion } from "./search-dialog-motion";

export function useSearchDialogMotion(expanded: boolean, onClosed: () => void) {
  const initialExpanded = useRef(expanded);
  const closed = useRef(onClosed);
  const overlay = useRef<HTMLDivElement | null>(null);
  const controller = useRef<ReturnType<typeof createSearchDialogMotion> | null>(
    null,
  );
  useLayoutEffect(() => {
    closed.current = onClosed;
  }, [onClosed]);
  const ref = useCallback((node: HTMLDivElement | null) => {
    controller.current?.dispose();
    controller.current = node
      ? createSearchDialogMotion(
          node,
          overlay.current,
          initialExpanded.current,
          () => closed.current(),
        )
      : null;
  }, []);
  const overlayRef = useCallback((node: HTMLDivElement | null) => {
    overlay.current = node;
  }, []);
  useLayoutEffect(() => {
    controller.current?.setExpanded(expanded);
  }, [expanded]);
  return {
    ref,
    overlayRef,
    open: () => controller.current?.open(),
    close: () => controller.current?.close(),
  };
}
