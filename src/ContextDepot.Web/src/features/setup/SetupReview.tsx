import { useTranslation } from "react-i18next";
import { CheckIcon, FolderIcon } from "lucide-react";
import type { InferenceProviderSettings } from "@/features/settings/settings-api";
import { SubmitButton } from "@/components/content/SubmitButton";
import type { SetupWorkspace } from "./setup-api";
import { SetupStepFooter } from "./SetupStepFooter";

export function SetupReview({
  workspace,
  settings,
  keyName,
  pending,
  disabled = false,
  onComplete,
  onBack,
}: {
  workspace: SetupWorkspace;
  settings: InferenceProviderSettings;
  keyName: string | null;
  pending: boolean;
  disabled?: boolean;
  onComplete: () => void;
  onBack: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="setup-review">
      <div className="setup-workspace-summary">
        <span className="setup-summary-label">
          <FolderIcon aria-hidden="true" />
          {t("setupReviewWorkspace")}
        </span>
        <strong>{workspace.name}</strong>
        <code>{workspace.path}</code>
        {workspace.description && <p>{workspace.description}</p>}
      </div>
      <dl className="setup-review-list">
        {settings.routes.map((route) => (
          <div key={route.capability}>
            <dt>
              {t(
                route.capability === "embedding"
                  ? "settingsEmbeddingTitle"
                  : "settingsChatTitle",
              )}
            </dt>
            <dd>
              {route.providerId ? (
                <>
                  <strong>{route.providerName}</strong>
                  <code>{route.model}</code>
                  <span className="setup-review-later">
                    {t("setupDraftState")}
                  </span>
                </>
              ) : (
                <span className="setup-review-later">
                  {t("setupReviewLater")}
                </span>
              )}
            </dd>
          </div>
        ))}
        <div>
          <dt>{t("setupReviewKeys")}</dt>
          <dd>
            {keyName ? (
              <>
                <strong>{keyName}</strong>
                <span className="setup-review-later">
                  {t("setupKeyGenerateOnFinish")}
                </span>
              </>
            ) : (
              <span className="setup-review-later">
                {t("setupReviewLater")}
              </span>
            )}
          </dd>
        </div>
      </dl>
      <p className="setup-note">{t("setupAtomicWhy")}</p>
      <SetupStepFooter onBack={onBack} disabled={pending || disabled}>
        <SubmitButton
          icon={CheckIcon}
          type="button"
          pending={pending}
          disabled={disabled}
          label={t("setupFinish")}
          pendingLabel={t("setupFinishing")}
          onClick={onComplete}
        />
      </SetupStepFooter>
    </div>
  );
}
