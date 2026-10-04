import { useTranslation } from "react-i18next";
import { PageHeader } from "@/components/content/PageHeader";
import { CreateWorkspaceDialog } from "@/features/home/CreateWorkspaceDialog";
import { SpaceDirectory } from "@/features/spaces/SpaceDirectory";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";

export function Spaces() {
  const { t } = useTranslation();
  const spaces = useSpaceDirectory();
  return (
    <div className="spaces-workbench spaces-directory-page">
      <PageHeader
        eyebrow={t("spacesKicker")}
        title={t("spacesTitle")}
        description={t("spacesSub")}
        actions={<CreateWorkspaceDialog onCreated={spaces.onCreated} />}
      />
      <SpaceDirectory resource={spaces} />
    </div>
  );
}
