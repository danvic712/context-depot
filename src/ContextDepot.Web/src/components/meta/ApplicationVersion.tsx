import { useTranslation } from "react-i18next";
import "./ApplicationVersion.css";

export function ApplicationVersion({
  version,
  pending,
  failed,
  showLabel = true,
}: {
  version: string | null | undefined;
  pending: boolean;
  failed: boolean;
  showLabel?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <span className="application-version" aria-live="polite">
      {showLabel && <>{t("appVersion")}: </>}
      {failed ? (
        t("appVersionUnavailable")
      ) : version ? (
        <code>{version}</code>
      ) : pending ? (
        t("appVersionLoading")
      ) : (
        t("appVersionMissing")
      )}
    </span>
  );
}
