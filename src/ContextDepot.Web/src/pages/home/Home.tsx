import { ItemGroup } from "@/components/ui/item";
import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ArrowRightIcon, FileTextIcon, FolderIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { WorkspaceCollection } from "@/features/spaces/WorkspaceCollection";
import "@/styles/home.css";
import { Brand } from "@/components/Brand";
import { Notice } from "@/components/content/PageElements";
import { StatePreview } from "@/components/feedback/StatePreview";
import { SearchBox } from "@/features/knowledge/SearchBox";
import { SampleRow } from "@/features/knowledge/KnowledgeRow";
import { sampleKnowledge } from "@/features/knowledge/sample-data";

export function Home() {
  const { preview, state, onRetry, onSearch } = useAppContext();
  const { t } = useTranslation();
  return (
    <>
      <div className="hero">
        <span className="kicker">KNOWLEDGE OS</span>
        <h1>{t("hero")}</h1>
        <p>{t("heroSub")}</p>
        <SearchBox value="" onSearch={onSearch} />
      </div>
      <section className="home-section">
        <div className="section-title">
          <h2>{t("yourSpaces")}</h2>
          <Button variant="link" asChild className="home-spaces-link">
            <Link to={preview ? "/spaces?preview=1" : "/spaces"}>
              {t("spaces")}
              <ArrowRightIcon data-icon="inline-end" />
            </Link>
          </Button>
        </div>
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
      </section>
      <div className="home-bottom">
        <section className="home-section">
          <h2>{t("recent")}</h2>
          {preview && state === "degraded" && (
            <StatePreview state={state} onRetry={onRetry} />
          )}{" "}
          {preview && state !== "success" && state !== "degraded" ? (
            <StatePreview state={state} onRetry={onRetry} />
          ) : preview ? (
            <ItemGroup className="divide-y border-y">
              {sampleKnowledge.slice(0, 3).map((item) => (
                <SampleRow key={item.id} item={item} />
              ))}
            </ItemGroup>
          ) : (
            <Notice
              icon={FileTextIcon}
              title={t("recentMissing")}
              detail={t("recentWhy")}
            />
          )}
        </section>
        <aside className="story">
          <Brand />
          <h2>{t("story")}</h2>
          <p>{t("storyText")}</p>
          <div className="story-line" />
          <small>Capture / Organize / Understand</small>
        </aside>
      </div>
    </>
  );
}
