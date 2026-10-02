import type { ComponentProps } from "react";
import { LoaderCircleIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function SubmitButton({
  pending,
  label,
  pendingLabel,
  disabled,
  ...props
}: ComponentProps<typeof Button> & {
  pending: boolean;
  label: string;
  pendingLabel: string;
}) {
  return (
    <Button type="submit" {...props} disabled={pending || disabled}>
      <LoaderCircleIcon
        data-icon="inline-start"
        aria-hidden="true"
        className={cn(pending ? "animate-spin" : "invisible")}
      />
      <span className="action-label">
        <span aria-hidden="true" className="invisible">
          {label}
        </span>
        <span aria-hidden="true" className="invisible">
          {pendingLabel}
        </span>
        <span>{pending ? pendingLabel : label}</span>
      </span>
    </Button>
  );
}
