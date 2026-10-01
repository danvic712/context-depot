import {
  ArrowRightIcon,
  FileTextIcon,
  MessageSquareTextIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { Button } from "@/components/ui/button";
import { useAppContext } from "@/hooks/use-app-context";
import type { WorkspaceSummary } from "./home-api";

export function KnowledgeEmptyState({
  spaces,
}: {
  spaces?: WorkspaceSummary[];
}) {
  const { t } = useTranslation();
  const { linkTo } = useAppContext();
  const location = useLocation();
  const workspace = spaces?.[0];
  return (
    <div className="home-empty-knowledge">
      <div className="home-empty-knowledge-intro">
        <span className="home-empty-knowledge-icon" aria-hidden="true">
          <MessageSquareTextIcon />
        </span>
        <div>
          <h3>{t("homeKnowledgeEmpty")}</h3>
          <p>{t("homeKnowledgeEmptyHint")}</p>
        </div>
      </div>
      <div className="home-knowledge-prompts">
        <div>
          <MessageSquareTextIcon aria-hidden="true" />
          <h4>{t("homeSaveContext")}</h4>
          <p>{t("homeSaveContextHint")}</p>
          <span>{t("homeContextExample")}</span>
        </div>
        <div>
          <FileTextIcon aria-hidden="true" />
          <h4>{t("homeSaveDocument")}</h4>
          <p>{t("homeSaveDocumentHint")}</p>
          <span>{t("homeDocumentExample")}</span>
        </div>
      </div>
      <div className="home-knowledge-next">
        <p>
          {workspace
            ? t("homeChooseSpaceHint", {
                name: workspace.name,
                path: workspace.path,
              })
            : t(spaces ? "homeCreateBeforeSave" : "homeSaveThroughMcp")}
        </p>
        {workspace && (
          <Button variant="outline" asChild>
            <Link
              to={linkTo(`/spaces/${workspace.id}`)}
              state={{
                from: location.pathname + location.search,
                navigation: "home",
              }}
            >
              {t("homeOpenSpace")}
              <ArrowRightIcon aria-hidden="true" />
            </Link>
          </Button>
        )}
      </div>
    </div>
  );
}
