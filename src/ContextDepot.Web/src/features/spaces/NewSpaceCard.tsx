import "@/styles/workspace-card.css";
import { PlusIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { CreateWorkspaceDialog } from "@/features/home/CreateWorkspaceDialog";

export function NewSpaceCard({
  onCreated,
}: {
  onCreated: () => void | Promise<void>;
}) {
  const { t } = useTranslation();
  return (
    <CreateWorkspaceDialog
      onCreated={onCreated}
      triggerClassName="space-card space-create-card"
    >
      <span className="space-card-icon">
        <PlusIcon aria-hidden="true" />
      </span>
      <strong>{t("newSpace")}</strong>
      <span className="space-card-description">
        {t("createSpaceDescription")}
      </span>
      <span className="space-card-footer space-create-footer">
        {t("createSpaceExample")}
      </span>
    </CreateWorkspaceDialog>
  );
}
