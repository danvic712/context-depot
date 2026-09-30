import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { FolderIcon } from "lucide-react";
import { WorkspaceCollection } from "@/features/spaces/WorkspaceCollection";
import "@/styles/spaces.css";
import { Notice, Heading } from "@/components/content/PageElements";
import { StatePreview } from "@/components/feedback/StatePreview";

export function Spaces() {
  const { preview, state, onRetry } = useAppContext();
  const { t } = useTranslation();
  return (
    <>
      <Heading
        kicker={t("spacesKicker")}
        title={t("spacesTitle")}
        sub={t("spacesSub")}
      />
      <div className="spaces-content">
        {preview && state === "degraded" && (
          <StatePreview state={state} onRetry={onRetry} />
        )}{" "}
        {preview && state !== "success" && state !== "degraded" ? (
          <StatePreview state={state} onRetry={onRetry} />
        ) : preview ? (
          <WorkspaceCollection />
        ) : (
          <Notice
            icon={FolderIcon}
            title={t("spacesMissing")}
            detail={t("spacesWhy")}
          />
        )}
      </div>
    </>
  );
}
