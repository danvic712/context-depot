import { useTranslation } from "react-i18next";
import { CreateWorkspaceDialog } from "@/features/home/CreateWorkspaceDialog";
import { SpaceDirectory } from "@/features/spaces/SpaceDirectory";
import { useDirectoryPage } from "@/features/spaces/use-space-directory";
import { SpaceDirectoryFilters } from "@/features/spaces/SpaceDirectoryFilters";
import "@/styles/spaces.css";

export function Spaces() {
  const { t } = useTranslation();
  const directory = useDirectoryPage();
  return (
    <div className="spaces-workbench spaces-directory-page">
      <h1 className="sr-only">{t("spacesTitle")}</h1>
      <SpaceDirectoryFilters
        action={
          <CreateWorkspaceDialog onCreated={directory.resource.onCreated} />
        }
        filters={directory.filters}
        pending={directory.resource.pending}
        onApply={directory.apply}
        onReset={directory.reset}
      />
      <SpaceDirectory directory={directory} />
    </div>
  );
}
