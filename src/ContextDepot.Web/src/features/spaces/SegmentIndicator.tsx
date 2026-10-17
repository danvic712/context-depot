import { useLayoutEffect, useRef } from "react";
import { createSpring } from "@/lib/spring-motion";

/** Decoration only: selection, focus and keyboard behavior remain with Radix/Router. */
export function SegmentIndicator({
  value,
  selector = '[data-state="on"]',
}: {
  value: string;
  selector?: string;
}) {
  const indicator = useRef<HTMLSpanElement>(null);
  const update = useRef<() => void>(() => {});
  useLayoutEffect(() => {
    const element = indicator.current;
    const group = element?.parentElement;
    if (!element || !group) return;
    const initial = group
      .querySelector<HTMLElement>(selector)
      ?.getBoundingClientRect();
    let x = initial ? initial.left - group.getBoundingClientRect().left : 0;
    let width = initial?.width ?? 1,
      targetWidth = width;
    const paint = () => {
      element.style.transform = `translateX(${x}px) scaleX(${width / targetWidth})`;
    };
    const horizontal = createSpring(x, (value) => {
      x = value;
      paint();
    });
    const size = createSpring(width, (value) => {
      width = value;
      paint();
    });
    const measure = () => {
      const selected = group.querySelector<HTMLElement>(selector);
      if (!selected) {
        element.style.opacity = "0";
        return;
      }
      const bounds = group.getBoundingClientRect(),
        active = selected.getBoundingClientRect();
      targetWidth = active.width;
      element.style.width = `${targetWidth}px`;
      element.style.height = `${active.height}px`;
      element.style.top = `${active.top - bounds.top}px`;
      element.style.opacity = "1";
      horizontal.to(active.left - bounds.left);
      size.to(targetWidth);
    };
    // Initialize the first selection without travelling from an unrelated origin.
    measure();
    update.current = measure;
    const observer = new ResizeObserver(measure);
    observer.observe(group);
    return () => {
      observer.disconnect();
      horizontal.dispose();
      size.dispose();
    };
  }, [selector]);
  useLayoutEffect(() => {
    update.current();
  }, [value]);
  return (
    <span
      ref={indicator}
      className="space-segment-indicator"
      aria-hidden="true"
    />
  );
}
