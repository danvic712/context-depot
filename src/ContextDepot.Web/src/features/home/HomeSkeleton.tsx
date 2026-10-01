import { Skeleton } from "@/components/ui/skeleton";
import {
  HomeDashboardLayout,
  HomeGuideLayout,
  HomeHeroLayout,
} from "./HomeLayout";

export function WorkspaceSkeleton() {
  return (
    <div className="home-space-grid" aria-hidden="true">
      {Array.from({ length: 4 }, (_, index) => (
        <div className="home-space-tile home-space-skeleton" key={index}>
          <Skeleton className="size-9" />
          <Skeleton className="h-5 w-2/3" />
          <div className="home-skeleton-lines">
            <Skeleton className="h-3 w-full" />
            <Skeleton className="h-3 w-3/4" />
          </div>
          <Skeleton className="h-3 w-1/2" />
          <Skeleton className="h-3 w-3/4" />
        </div>
      ))}
    </div>
  );
}

export function KnowledgeSkeleton() {
  return (
    <div className="home-knowledge-list" aria-hidden="true">
      {Array.from({ length: 3 }, (_, index) => (
        <div className="home-knowledge-row" key={index}>
          <Skeleton className="size-10 shrink-0" />
          <div className="home-knowledge-copy">
            <Skeleton className="mb-3 h-4 w-2/3" />
            <Skeleton className="h-3 w-1/2" />
          </div>
          <Skeleton className="h-3 w-12 shrink-0" />
        </div>
      ))}
    </div>
  );
}

function GuideSkeleton() {
  return (
    <HomeGuideLayout title={<Skeleton className="h-5 w-4/5" />}>
      <ol className="home-guide-tips">
        {Array.from({ length: 4 }, (_, index) => (
          <li className="home-about-row" key={index}>
            <Skeleton className="size-5 shrink-0" />
            <div className="home-skeleton-lines grow">
              <Skeleton className="mb-1 h-4 w-3/4" />
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-4/5" />
              {index > 0 && <Skeleton className="mt-1 h-3 w-1/2" />}
            </div>
          </li>
        ))}
      </ol>
      <div className="home-mcp-guide home-skeleton-lines">
        <Skeleton className="mb-1 h-5 w-2/3" />
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-3/4" />
        <dl>
          {Array.from({ length: 2 }, (_, index) => (
            <div className="home-skeleton-lines" key={index}>
              <dt>
                <Skeleton className="h-3 w-1/3" />
              </dt>
              <dd>
                <Skeleton className="h-3 w-4/5" />
              </dd>
            </div>
          ))}
        </dl>
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-3/4" />
      </div>
    </HomeGuideLayout>
  );
}

export function HomeHeroSkeleton() {
  return (
    <div aria-hidden="true">
      <Skeleton className="home-eyebrow-skeleton" />
      <div className="home-hero-title">
        <Skeleton className="home-title-skeleton" />
      </div>
      <div className="home-hero-subtitle">
        <Skeleton className="home-subtitle-skeleton" />
      </div>
    </div>
  );
}

export function HomeSkeleton() {
  return (
    <div className="home-skeleton" aria-hidden="true">
      <HomeHeroLayout
        search={
          <div className="search-box">
            <div className="search-input-group home-search-skeleton">
              <Skeleton className="size-5 shrink-0" />
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="home-search-button-skeleton" />
            </div>
          </div>
        }
      >
        <HomeHeroSkeleton />
      </HomeHeroLayout>
      <HomeDashboardLayout guide={<GuideSkeleton />}>
        <section className="home-section">
          <div className="home-section-title">
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-4 w-24" />
          </div>
          <WorkspaceSkeleton />
        </section>
        <section className="home-section">
          <div className="home-section-title">
            <Skeleton className="h-5 w-44" />
            <Skeleton className="h-3 w-20" />
          </div>
          <KnowledgeSkeleton />
        </section>
      </HomeDashboardLayout>
    </div>
  );
}
