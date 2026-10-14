import type { ComponentProps } from "react";
import type { LucideIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

export function SubmitButton({
  pending,
  label,
  pendingLabel,
  icon: Icon,
  disabled,
  ...props
}: ComponentProps<typeof Button> & {
  pending: boolean;
  label: string;
  pendingLabel: string;
  icon?: LucideIcon;
}) {
  return (
    <Button
      type="submit"
      {...props}
      disabled={pending || disabled}
      aria-busy={pending}
    >
      {pending || !Icon ? (
        <Spinner
          data-icon="inline-start"
          aria-hidden="true"
          className={cn(!pending && "invisible")}
        />
      ) : (
        <Icon data-icon="inline-start" aria-hidden="true" />
      )}
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
