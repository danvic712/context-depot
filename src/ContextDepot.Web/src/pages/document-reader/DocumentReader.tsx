import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { FileTextIcon } from "lucide-react";
import { Notice } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";

export function DocumentReader() {
  const { onBack } = useAppContext();
  const { t } = useTranslation();
  return (
    <>
      <Button type="button" variant="ghost" className="mb-2" onClick={onBack}>
        ← {t("back")}
      </Button>
      <Notice
        icon={FileTextIcon}
        title={t("recentMissing")}
        detail={t("recentWhy")}
      />
    </>
  );
}
