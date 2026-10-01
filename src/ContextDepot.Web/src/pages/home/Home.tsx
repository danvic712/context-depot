import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ArrowRightIcon } from "lucide-react";
import { useAppContext } from "@/hooks/use-app-context";
import { Button } from "@/components/ui/button";
import { SearchBox } from "@/features/knowledge/SearchBox";
import { StatePreview } from "@/components/feedback/StatePreview";
import { getKnowledge, getWorkspaces } from "@/features/home/home-api";
import { useResourceCollection } from "@/features/home/use-resource-collection";
import { KnowledgeEmptyState } from "@/features/home/KnowledgeEmptyState";
import { HomeGuide } from "@/features/home/HomeGuide";
import { HomeHeroSkeleton } from "@/features/home/HomeSkeleton";
import {
  CollectionError,
  RecentKnowledge,
  WorkspaceTiles,
} from "@/features/home/HomeCollections";
import {
  previewKnowledge,
  previewWorkspaces,
} from "@/features/home/preview-data";
import {
  HomeDashboardLayout,
  HomeHeroLayout,
} from "@/features/home/HomeLayout";

export function Home() {
  const { preview, state, onRetry, onSearch } = useAppContext();
  const { t } = useTranslation();
  const spaces = useResourceCollection(getWorkspaces, !preview);
  const knowledge = useResourceCollection(getKnowledge, !preview);
  const previewError = preview && (state === "error" || state === "permission");
  const empty = preview && state === "empty";
  const spacesPending = preview
    ? state === "loading"
    : !spaces.data && spaces.pending && !spaces.error;
  const knowledgePending = preview
    ? state === "loading"
    : !knowledge.data && knowledge.pending && !knowledge.error;
  const spacesEmpty =
    !spacesPending &&
    !spaces.error &&
    (empty || (!preview && spaces.data?.items.length === 0));
  const spaceItems = empty
    ? []
    : preview
      ? previewWorkspaces
      : (spaces.data?.items ?? []);
  const knowledgeItems = empty
    ? []
    : preview
      ? previewKnowledge
      : (knowledge.data?.items ?? []);
  const knowledgeEmpty =
    !knowledgePending &&
    !previewError &&
    !knowledge.error &&
    knowledgeItems.length === 0;
  return (
    <>
      {(spacesPending || knowledgePending) && (
        <span className="sr-only" role="status">
          {t("loading")}
        </span>
      )}
      <HomeHeroLayout search={<SearchBox value="" onSearch={onSearch} home />}>
        {knowledgePending ? (
          <HomeHeroSkeleton />
        ) : (
          <div className="home-hero-ready">
            <span className="home-eyebrow">{t("homeEyebrow")}</span>
            <h1 className="home-hero-title">
              {t(knowledgeEmpty ? "homeEmptyHero" : "hero")}
            </h1>
            <p className="home-hero-subtitle">
              {t(knowledgeEmpty ? "homeEmptyHeroSub" : "heroSub")}
            </p>
          </div>
        )}
      </HomeHeroLayout>
      <HomeDashboardLayout guide={<HomeGuide />}>
        <section
          className="home-section"
          aria-labelledby="home-spaces-title"
          aria-busy={preview ? spacesPending : spaces.pending}
        >
          <div className="home-section-title">
            <h2 id="home-spaces-title">{t("yourSpaces")}</h2>
            <Button variant="link" asChild>
              <Link to={preview ? "/spaces?preview=1" : "/spaces"}>
                <span>{t("homeViewSpaces")}</span>
                <ArrowRightIcon aria-hidden="true" />
              </Link>
            </Button>
          </div>
          <div className="home-section-content" data-loading={spacesPending}>
            {previewError ? (
              <CollectionError
                title={t("homeSpacesLoadError")}
                failure={{
                  kind: state === "permission" ? "forbidden" : "network",
                  retryable: state !== "permission",
                }}
                onRetry={onRetry}
              />
            ) : (
              <>
                {spaces.error && (
                  <CollectionError
                    title={t("homeSpacesLoadError")}
                    failure={spaces.error}
                    pending={spaces.pending}
                    stale={!!spaces.data}
                    onRetry={spaces.refresh}
                  />
                )}
                {(!spaces.error || spaces.data) && (
                  <WorkspaceTiles
                    items={spaceItems}
                    pending={spacesPending}
                    empty={spacesEmpty}
                    onCreated={spaces.refresh}
                    preview={preview}
                  />
                )}
              </>
            )}
          </div>
        </section>
        <section
          className="home-section"
          aria-labelledby="home-knowledge-title"
          aria-busy={preview ? knowledgePending : knowledge.pending}
        >
          <div className="home-section-title">
            <h2 id="home-knowledge-title">{t("recent")}</h2>
            {!knowledgeEmpty && (
              <span className="home-section-caption">
                {t("homeRecentlyUpdated")}
              </span>
            )}
          </div>
          <div className="home-section-content" data-loading={knowledgePending}>
            {previewError ? (
              <CollectionError
                title={t("homeKnowledgeLoadError")}
                failure={{
                  kind: state === "permission" ? "forbidden" : "network",
                  retryable: state !== "permission",
                }}
                onRetry={onRetry}
              />
            ) : (
              <>
                {preview && state === "degraded" && (
                  <StatePreview state={state} onRetry={onRetry} />
                )}
                {knowledge.error && (
                  <CollectionError
                    title={t("homeKnowledgeLoadError")}
                    failure={knowledge.error}
                    pending={knowledge.pending}
                    stale={!!knowledge.data}
                    onRetry={knowledge.refresh}
                  />
                )}
                {knowledgeEmpty ? (
                  <KnowledgeEmptyState
                    spaces={
                      spacesPending ||
                      previewError ||
                      (spaces.error && !spaces.data)
                        ? undefined
                        : spaceItems
                    }
                  />
                ) : (
                  (!knowledge.error || knowledge.data) && (
                    <RecentKnowledge
                      pending={knowledgePending}
                      items={knowledgeItems}
                    />
                  )
                )}
              </>
            )}
          </div>
        </section>
      </HomeDashboardLayout>
    </>
  );
}
