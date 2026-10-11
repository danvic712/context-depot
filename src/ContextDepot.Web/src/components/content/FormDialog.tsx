import type { ComponentProps, FormEventHandler, ReactNode, Ref } from "react";
import { useRef } from "react";
import { useTranslation } from "react-i18next";
import type { LucideIcon } from "lucide-react";
import {
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { FieldGroup } from "@/components/ui/field";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { cn } from "@/lib/utils";
import "@/styles/form-dialog.css";

export function FormDialog({
  title,
  titleIcon: TitleIcon,
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
  titleIcon?: LucideIcon;
  description: string;
  pending?: boolean;
  preventDismiss?: boolean;
  variant?: "default" | "wide";
  footer: ReactNode;
  error?: string;
  onSubmit?: FormEventHandler<HTMLFormElement>;
  formRef?: Ref<HTMLFormElement>;
}) {
  const { t } = useTranslation();
  const returnFocus = useRef<HTMLElement | null>(null);
  const content = (
    <>
      <div className="form-dialog-body">
        <FieldGroup className="form-dialog-fields">{children}</FieldGroup>
      </div>
      {error && (
        <RequestFeedback
          className="form-dialog-feedback"
          title={t("requestActionError")}
          description={error}
          pending={pending}
          compact
        />
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
      onOpenAutoFocus={(event) => {
        const active = document.activeElement;
        returnFocus.current = active instanceof HTMLElement ? active : null;
        props.onOpenAutoFocus?.(event);
      }}
      onCloseAutoFocus={(event) => {
        props.onCloseAutoFocus?.(event);
        if (!event.defaultPrevented && returnFocus.current?.isConnected) {
          event.preventDefault();
          returnFocus.current.focus({ preventScroll: true });
        }
      }}
      onEscapeKeyDown={(event) => {
        if (pending || preventDismiss) event.preventDefault();
      }}
      onPointerDownOutside={(event) => {
        if (pending || preventDismiss) event.preventDefault();
      }}
    >
      <DialogHeader className="form-dialog-header">
        <DialogTitle className="form-dialog-title-row">
          {TitleIcon && <TitleIcon aria-hidden="true" />}
          {title}
        </DialogTitle>
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
