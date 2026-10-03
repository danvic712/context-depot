import { PageHeaderSkeleton } from "@/components/content/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import "@/styles/settings.css";

export function SettingsResourceSkeleton() {
  return (
    <div className="settings-loading" aria-hidden="true">
      <Skeleton className="h-5 w-1/3" />
      <Skeleton className="h-12 w-full" />
      <Skeleton className="h-5 w-2/3" />
    </div>
  );
}

export function SettingsSkeleton() {
  return (
    <div className="settings settings-page-skeleton" aria-hidden="true">
      <PageHeaderSkeleton />
      <div className="settings-layout">
        <div className="section-nav">
          {Array.from({ length: 7 }, (_, i) => (
            <Skeleton key={i} className="my-3 h-6 w-20 shrink-0" />
          ))}
        </div>
        <div className="settings-stack">
          {Array.from({ length: 3 }, (_, i) => (
            <section className="settings-section" key={i}>
              <div className="settings-section-header">
                <Skeleton className="settings-section-icon" />
                <div className="settings-section-heading">
                  <Skeleton className="mb-2 h-5 w-32" />
                  <Skeleton className="h-4 w-2/3" />
                </div>
              </div>
              <div className="settings-section-content">
                <SettingsResourceSkeleton />
              </div>
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}
