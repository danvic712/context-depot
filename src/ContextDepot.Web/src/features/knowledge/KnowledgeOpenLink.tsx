import { forwardRef, type ComponentProps } from "react";
import { Link } from "react-router";
import {
  isReadingClick,
  useKnowledgeReading,
  type ReadingTarget,
} from "./knowledge-reading-context";

export const KnowledgeOpenLink = forwardRef<
  HTMLAnchorElement,
  ComponentProps<typeof Link> & { item: ReadingTarget }
>(function KnowledgeOpenLink({ item, onClick, ...props }, ref) {
  const reading = useKnowledgeReading();
  return (
    <Link
      {...props}
      ref={ref}
      onClick={(event) => {
        onClick?.(event);
        if (
          reading &&
          isReadingClick(event) &&
          (!props.target || props.target === "_self")
        ) {
          event.preventDefault();
          reading.open(item);
        }
      }}
    />
  );
});
