import { useTranslation } from "react-i18next";

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
      <details open>
        <summary>{t("settingsDimensionsMore")}</summary>
        <p>{t("settingsDimensionsChangeHint")}</p>
      </details>
    </aside>
  );
}
