import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { ArrowRightIcon } from "lucide-react";
import { PageHeader } from "@/components/content/PageHeader";
import { Button } from "@/components/ui/button";
import { SearchBox } from "@/features/knowledge/SearchBox";
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
  HomeDashboardLayout,
  HomeHeroLayout,
} from "@/features/home/HomeLayout";

export function Home() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const spaces = useResourceCollection(getWorkspaces);
  const knowledge = useResourceCollection(getKnowledge);
  const spacesPending = !spaces.data && spaces.pending && !spaces.error;
  const knowledgePending =
    !knowledge.data && knowledge.pending && !knowledge.error;
  const spacesEmpty =
    !spacesPending && !spaces.error && spaces.data?.items.length === 0;
  const spaceItems = spaces.data?.items ?? [];
  const knowledgeItems = knowledge.data?.items ?? [];
  const knowledgeEmpty =
    !knowledgePending && !knowledge.error && knowledgeItems.length === 0;
  return (
    <>
      {(spacesPending || knowledgePending) && (
        <span className="sr-only" role="status">
          {t("loading")}
        </span>
      )}
      <HomeHeroLayout
        search={
          <SearchBox
            value=""
            home
            onSearch={(query) => {
              void navigate(`/search?${new URLSearchParams({ q: query })}`, {
                state: { focusSearch: true },
              });
            }}
          />
        }
      >
        {knowledgePending ? (
          <HomeHeroSkeleton />
        ) : (
          <PageHeader
            variant="welcome"
            eyebrow={t("homeEyebrow")}
            title={t(knowledgeEmpty ? "homeEmptyHero" : "hero")}
            description={t(knowledgeEmpty ? "homeEmptyHeroSub" : "heroSub")}
          />
        )}
      </HomeHeroLayout>
      <HomeDashboardLayout guide={<HomeGuide />}>
        <section
          className="home-section"
          aria-labelledby="home-spaces-title"
          aria-busy={spaces.pending}
        >
          <div className="home-section-title">
            <h2 id="home-spaces-title">{t("yourSpaces")}</h2>
            <Button variant="link" asChild>
              <Link to="/spaces">
                <span>{t("homeViewSpaces")}</span>
                <ArrowRightIcon aria-hidden="true" />
              </Link>
            </Button>
          </div>
          <div className="home-section-content" data-loading={spacesPending}>
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
              />
            )}
          </div>
        </section>
        <section
          className="home-section"
          aria-labelledby="home-knowledge-title"
          aria-busy={knowledge.pending}
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
                  spacesPending || (spaces.error && !spaces.data)
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
          </div>
        </section>
      </HomeDashboardLayout>
    </>
  );
}
