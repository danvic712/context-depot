import "@/styles/knowledge-delete.css";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "axios";
import { toast } from "sonner";
import { Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogTrigger,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogCancel,
} from "@/components/ui/alert-dialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";
import { deleteKnowledge } from "./knowledge-management-api";

export function DeleteKnowledgeDialog({
  item,
  onDeleted,
  onRefresh,
  disabled = false,
  iconOnly = true,
}: {
  item: Pick<KnowledgePreview, "id" | "type" | "title" | "updatedAt"> & {
    path?: string;
  };
  onDeleted: () => void;
  onRefresh: () => void;
  disabled?: boolean;
  iconOnly?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [changed, setChanged] = useState(false);
  const request = useRef<AbortController>(null);
  useEffect(() => () => request.current?.abort(), []);
  async function remove() {
    if (pending || changed) return;
    const controller = new AbortController();
    request.current = controller;
    setPending(true);
    setError(undefined);
    try {
      await deleteKnowledge(item, controller.signal);
      if (controller.signal.aborted) return;
      setOpen(false);
      toast.success(t("spacesDocumentDeleted"));
      onDeleted();
    } catch (error) {
      if (controller.signal.aborted) return;
      if (isAxiosError(error) && error.response?.status === 404) {
        setOpen(false);
        toast.info(t("spacesDocumentAlreadyDeleted"));
        onDeleted();
      } else {
        const conflict = isAxiosError(error) && error.response?.status === 409;
        setChanged(conflict);
        setError(t(conflict ? "spacesDeleteChanged" : "spacesDeleteFailedWhy"));
      }
    } finally {
      if (!controller.signal.aborted) setPending(false);
    }
  }
  return (
    <AlertDialog
      open={open}
      onOpenChange={(value) => {
        if (pending) return;
        setOpen(value);
        if (value) {
          setError(undefined);
          setChanged(false);
        }
      }}
    >
      <AlertDialogTrigger asChild>
        <Button
          variant={iconOnly ? "ghost" : "outline"}
          size={iconOnly ? "icon" : "sm"}
          className={iconOnly ? "archived-document-delete" : undefined}
          disabled={disabled}
          aria-label={t("spacesDeleteDocument", { title: item.title })}
          title={t("spacesDeleteDocument", { title: item.title })}
        >
          <Trash2Icon data-icon="inline-start" aria-hidden="true" />
          {!iconOnly && t("spacesDeleteAction")}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent
        className="archived-document-confirm"
        onEscapeKeyDown={(event) => {
          if (pending) event.preventDefault();
        }}
      >
        <AlertDialogHeader>
          <AlertDialogTitle>{t("spacesDeleteTitle")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("spacesDeleteDescription", { title: item.title })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        {item.path && (
          <code className="archived-document-confirm-path">{item.path}</code>
        )}
        {error && (
          <RequestFeedback
            compact
            title={t("spacesDeleteFailed")}
            description={error}
            recoveryAction={{
              label: t("spacesRefreshArchive"),
              onClick: () => {
                setOpen(false);
                onRefresh();
              },
            }}
          />
        )}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>
            {t("cancel")}
          </AlertDialogCancel>
          <SubmitButton
            type="button"
            variant="destructive"
            disabled={changed}
            pending={pending}
            icon={Trash2Icon}
            label={t("spacesDeletePermanently")}
            pendingLabel={t("spacesDeletingDocument")}
            onClick={() => void remove()}
          />
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
