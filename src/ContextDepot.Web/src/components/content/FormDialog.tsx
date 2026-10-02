import type { ComponentProps, FormEventHandler, ReactNode, Ref } from "react";
import {
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { FieldGroup } from "@/components/ui/field";
import { cn } from "@/lib/utils";
import "@/styles/form-dialog.css";

export function FormDialog({
  title,
  description,
  closeLabel,
  pending = false,
  preventDismiss = false,
  variant = "default",
  footer,
  error,
  children,
  onSubmit,
  formRef,
  ...props
}: Omit<
  ComponentProps<typeof DialogContent>,
  "title" | "closeDisabled" | "onEscapeKeyDown" | "onPointerDownOutside"
> & {
  title: string;
  description: string;
  pending?: boolean;
  preventDismiss?: boolean;
  variant?: "default" | "wide";
  footer: ReactNode;
  error?: string;
  onSubmit?: FormEventHandler<HTMLFormElement>;
  formRef?: Ref<HTMLFormElement>;
}) {
  const content = (
    <>
      <div className="form-dialog-body">
        <FieldGroup className="form-dialog-fields">{children}</FieldGroup>
      </div>
      {error && (
        <p className="form-dialog-feedback" role="alert">
          {error}
        </p>
      )}
      <DialogFooter className="form-dialog-footer">{footer}</DialogFooter>
    </>
  );
  return (
    <DialogContent
      {...props}
      className={cn(
        "form-dialog",
        variant === "wide" && "form-dialog-wide",
        props.className,
      )}
      closeLabel={closeLabel}
      closeDisabled={pending}
      onEscapeKeyDown={(event) => {
        if (pending || preventDismiss) event.preventDefault();
      }}
      onPointerDownOutside={(event) => {
        if (pending || preventDismiss) event.preventDefault();
      }}
    >
      <DialogHeader className="form-dialog-header">
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>{description}</DialogDescription>
      </DialogHeader>
      {onSubmit ? (
        <form
          ref={formRef}
          className="form-dialog-form"
          onSubmit={onSubmit}
          noValidate
          aria-busy={pending}
        >
          {content}
        </form>
      ) : (
        <div className="form-dialog-form">{content}</div>
      )}
    </DialogContent>
  );
}
