import type { ReactNode } from "react";
import { ChevronLeftIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";

export function SetupStepFooter({
  onBack,
  disabled,
  children,
}: {
  onBack: () => void;
  disabled: boolean;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <div className="setup-step-footer">
      <Button variant="outline" disabled={disabled} onClick={onBack}>
        <ChevronLeftIcon data-icon="inline-start" aria-hidden="true" />
        {t("setupBack")}
      </Button>
      <div className="setup-action-group">{children}</div>
    </div>
  );
}
