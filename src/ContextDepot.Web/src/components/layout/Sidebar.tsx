import { HouseIcon, SearchIcon, FolderIcon, SettingsIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useMatches, useNavigation } from "react-router";
import type { NavigationItem, PageHandle } from "@/hooks/use-app-context";
import { cn } from "@/lib/utils";
import { lazy, Suspense } from "react";
import { Brand } from "../Brand";
import { Skeleton } from "../ui/skeleton";
import "@/styles/sidebar.css";

const ReadinessStatus = lazy(() =>
  import("@/features/home/ReadinessStatus").then((module) => ({
    default: module.ReadinessStatus,
  })),
);

const items = [
  { item: "home", path: "/", Icon: HouseIcon },
  { item: "search", path: "/search", Icon: SearchIcon },
  { item: "spaces", path: "/spaces", Icon: FolderIcon },
  { item: "settings", path: "/settings", Icon: SettingsIcon },
] as const;

export function Sidebar() {
  const { t } = useTranslation();
  const handle = (useMatches().at(-1)?.handle ?? {
    page: "home",
    navigation: "home",
  }) as PageHandle;
  const { state } = useLocation();
  const navigation = useNavigation();
  let active: NavigationItem | undefined =
    handle.page === "notFound" ? undefined : handle.navigation;
  if (
    (handle.page === "context" || handle.page === "document") &&
    (state?.navigation === "home" ||
      state?.navigation === "search" ||
      state?.navigation === "spaces")
  ) {
    active = state.navigation;
  }

  return (
    <aside className="rail">
      <div className="rail-brand">
        <Link
          className="rail-brand-link"
          to="/"
          aria-label={`ContextDepot · ${t("home")}`}
          title={t("home")}
        >
          <Brand />
        </Link>
      </div>
      <nav className="rail-links" aria-label={t("primaryNavigation")}>
        {items.map(({ item, path, Icon }) => {
          const pending = navigation.location?.pathname === path;
          return (
            <Link
              key={item}
              to={path}
              aria-current={active === item ? "page" : undefined}
              className={cn(
                "rail-link",
                active === item && "selected",
                pending && "pending",
                item === "settings" && "rail-settings",
              )}
            >
              <Icon size={22} aria-hidden="true" />
              <span>{t(item)}</span>
              {pending && (
                <span className="sr-only" role="status">
                  {t("loading")}
                </span>
              )}
            </Link>
          );
        })}
      </nav>
      <Suspense
        fallback={<div className="rail-status">{t("homeUnknown")}</div>}
      >
        <ReadinessStatus />
      </Suspense>
    </aside>
  );
}

export function SidebarSkeleton() {
  return (
    <aside className="rail" aria-hidden="true">
      <div className="rail-brand">
        <Brand />
      </div>
      <div className="rail-links">
        {items.map(({ item }) => (
          <div
            key={item}
            className={cn("rail-link", item === "settings" && "rail-settings")}
          >
            <Skeleton className="size-6" />
            <Skeleton className="h-3 w-10" />
          </div>
        ))}
      </div>
    </aside>
  );
}
