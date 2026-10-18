import { useTranslation } from "react-i18next";
import { ChevronDownIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from "@/components/ui/collapsible";

export function EmbeddingDimensionsHelp({ id }: { id: string }) {
  const { t } = useTranslation();
  return (
    <aside id={id} className="inference-dimensions-help settings-form-full">
      <p>{t("settingsDimensionsHint")}</p>
      <dl>
        <div>
          <dt>
            <code>text-embedding-3-small</code>
          </dt>
          <dd>
            <strong>1536</strong>
            <span>{t("settingsDimensionsDefault")}</span>
          </dd>
        </div>
        <div>
          <dt>
            <code>text-embedding-3-large</code>
          </dt>
          <dd>
            <strong>3072</strong>
            <span>{t("settingsDimensionsDefault")}</span>
          </dd>
        </div>
      </dl>
      <p>{t("settingsDimensionsOtherModels")}</p>
      <Collapsible defaultOpen className="inference-dimensions-details">
        <CollapsibleTrigger asChild>
          <Button
            type="button"
            variant="link"
            size="sm"
            className="settings-disclosure-trigger"
          >
            {t("settingsDimensionsMore")}
            <ChevronDownIcon data-icon="inline-end" aria-hidden="true" />
          </Button>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <p>{t("settingsDimensionsChangeHint")}</p>
        </CollapsibleContent>
      </Collapsible>
    </aside>
  );
}
