import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { FolderIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/content/PageElements";

export function SpaceDetail() {
  const { t } = useTranslation();
  return (
    <>
      <Button variant="ghost" className="mb-2" asChild>
        <Link to="/spaces">← {t("spaces")}</Link>
      </Button>
      <Notice
        icon={FolderIcon}
        title={t("spacesMissing")}
        detail={t("spacesWhy")}
      />
    </>
  );
}
