import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

export function SearchDialogFailure({
  onClose,
  onRestoreFocus,
}: {
  onClose: () => void;
  onRestoreFocus: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        closeLabel={t("dialogClose")}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          onRestoreFocus();
        }}
      >
        <DialogTitle>{t("pageLoadError")}</DialogTitle>
        <DialogDescription className="my-4">
          {t("pageLoadErrorWhy")}
        </DialogDescription>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => window.location.reload()}>
            {t("reload")}
          </Button>
          <Button asChild variant="outline">
            <Link to="/search" onClick={onClose}>
              {t("searchOpenPage")}
            </Link>
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
