import { useTranslation } from "react-i18next";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";

export function DiscardDraft({
  disabled,
  onDiscard,
  editorId,
}: {
  disabled: boolean;
  onDiscard: () => void;
  editorId: string;
}) {
  const { t } = useTranslation();
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button type="button" variant="outline" disabled={disabled}>
          {t("discardDraft")}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent
        onCloseAutoFocus={(event) => {
          if (disabled) {
            event.preventDefault();
            document.getElementById(editorId)?.focus();
          }
        }}
      >
        <AlertDialogHeader>
          <AlertDialogTitle>{t("discardDraft")}</AlertDialogTitle>
          <AlertDialogDescription>{t("discardWhy")}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t("cancel")}</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onDiscard}>
            {t("discardDraft")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
