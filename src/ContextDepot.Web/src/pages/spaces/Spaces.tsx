import { useTranslation } from "react-i18next";
import { FolderIcon } from "lucide-react";
import "@/styles/spaces.css";
import { Notice, Heading } from "@/components/content/PageElements";

export function Spaces() {
  const { t } = useTranslation();
  return (
    <>
      <Heading
        kicker={t("spacesKicker")}
        title={t("spacesTitle")}
        sub={t("spacesSub")}
      />
      <div className="spaces-content">
        <Notice
          icon={FolderIcon}
          title={t("spacesMissing")}
          detail={t("spacesWhy")}
        />
      </div>
    </>
  );
}
