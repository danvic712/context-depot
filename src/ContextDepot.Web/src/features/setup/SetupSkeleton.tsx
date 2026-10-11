import { Skeleton } from "@/components/ui/skeleton";
import "@/styles/setup.css";

export function SetupSkeleton() {
  return (
    <div className="setup-frame frame startup-loading" aria-busy="true">
      <div className="topbar" aria-hidden="true">
        <div className="brand-name">
          <Skeleton className="size-7" />
          <Skeleton className="h-5 w-28" />
        </div>
        <div className="top-controls">
          <Skeleton className="loading-preference" />
          <Skeleton className="loading-preference" />
        </div>
      </div>
      <main
        className="page setup-main"
        role="status"
        aria-label="Loading / 加载中"
      >
        <div className="setup-layout" data-step="workspace" aria-hidden="true">
          <aside className="setup-sidebar">
            <div className="setup-introduction">
              <Skeleton className="mb-3 h-3 w-20" />
              <Skeleton className="mb-3 h-8 w-4/5" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="mt-2 h-4 w-3/4" />
            </div>
            <ol className="setup-steps">
              {Array.from({ length: 4 }, (_, index) => (
                <li key={index}>
                  <div className="setup-step-placeholder">
                    <Skeleton className="size-7 shrink-0 rounded-full" />
                    <div className="w-full min-w-0 flex-1">
                      <Skeleton className="h-4 w-full max-w-20" />
                      <Skeleton className="setup-step-placeholder-detail mt-1 h-3 w-full" />
                    </div>
                  </div>
                </li>
              ))}
            </ol>
            <div className="setup-resume">
              <Skeleton className="size-4 shrink-0" />
              <div className="min-w-0 flex-1">
                <Skeleton className="h-3 w-full" />
                <Skeleton className="mt-2 h-3 w-4/5" />
              </div>
            </div>
          </aside>
          <div className="setup-content">
            <div className="setup-page-heading">
              <Skeleton className="setup-heading-icon" />
              <div className="setup-heading-copy w-full">
                <Skeleton className="h-3 w-24" />
                <Skeleton className="mt-3 h-8 w-3/4" />
                <Skeleton className="mt-3 h-5 w-full" />
              </div>
            </div>
            <div className="setup-workspace-form">
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
              <Skeleton className="setup-workspace-description h-28 w-full" />
              <Skeleton className="setup-workspace-actions h-12 w-full" />
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
