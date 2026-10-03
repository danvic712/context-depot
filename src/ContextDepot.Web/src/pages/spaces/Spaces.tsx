import { useTranslation } from "react-i18next";
import { PageHeader } from "@/components/content/PageHeader";
import { NewSpaceCard } from "@/features/spaces/NewSpaceCard";
import { SpaceDirectory } from "@/features/spaces/SpaceDirectory";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";

export function Spaces() {
  const { t } = useTranslation();
  const spaces = useSpaceDirectory();
  return (
    <div className="spaces-workbench">
      <PageHeader
        eyebrow={t("spacesKicker")}
        title={t("spacesTitle")}
        description={t("spacesSub")}
      />
      <SpaceDirectory
        resource={spaces}
        root
        creation={<NewSpaceCard onCreated={spaces.onCreated} />}
      />
    </div>
  );
}
