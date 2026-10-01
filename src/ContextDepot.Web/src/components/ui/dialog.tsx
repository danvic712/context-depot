import type { ComponentProps } from "react";
import { Dialog as Primitive } from "radix-ui";
import { XIcon } from "lucide-react";
import { cn } from "@/lib/utils";

export const Dialog = Primitive.Root;
export const DialogTrigger = Primitive.Trigger;
export const DialogClose = Primitive.Close;
export const DialogTitle = Primitive.Title;
export const DialogDescription = Primitive.Description;
export function DialogContent({
  className,
  children,
  closeLabel,
  closeDisabled = false,
  showCloseButton = true,
  overlayClassName,
  ...props
}: ComponentProps<typeof Primitive.Content> & {
  closeLabel?: string;
  showCloseButton?: boolean;
  overlayClassName?: string;
  closeDisabled?: boolean;
}) {
  return (
    <Primitive.Portal>
      <Primitive.Overlay
        className={cn(
          "fixed inset-0 z-50 bg-black/40 backdrop-blur-sm",
          overlayClassName,
        )}
      />
      <Primitive.Content
        className={cn(
          "fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 rounded-xl border bg-background p-7 shadow-xl max-h-[calc(100dvh-2rem)] overflow-y-auto",
          className,
        )}
        {...props}
      >
        {children}
        {showCloseButton && (
          <Primitive.Close
            className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-md border hover:bg-accent"
            aria-label={closeLabel}
            disabled={closeDisabled}
          >
            <XIcon size={16} aria-hidden="true" />
          </Primitive.Close>
        )}
      </Primitive.Content>
    </Primitive.Portal>
  );
}

export function DialogHeader({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("flex flex-col gap-2", className)} {...props} />;
}
export function DialogFooter({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      className={cn(
        "flex flex-col-reverse gap-2 sm:flex-row sm:justify-end",
        className,
      )}
      {...props}
    />
  );
}
