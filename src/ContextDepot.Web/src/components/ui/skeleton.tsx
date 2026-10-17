import { cn } from "@/lib/utils";
import "@/styles/skeleton.css";

function Skeleton({
  className,
  as: Component = "div",
  ...props
}: React.ComponentProps<"div"> & { as?: "div" | "span" }) {
  return (
    <Component
      data-slot="skeleton"
      className={cn("skeleton rounded-md", className)}
      {...props}
    />
  );
}

export { Skeleton };
