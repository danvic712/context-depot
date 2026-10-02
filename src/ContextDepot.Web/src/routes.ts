import type { RouteObject } from "react-router";
import App from "./App";
import { AppLoading, RouteError } from "@/components/feedback/RouteFeedback";
import { RouteRedirect } from "@/components/layout/RouteRedirect";
import type { PageHandle } from "@/hooks/use-app-context";

type PageRoute = RouteObject & { handle: PageHandle };

export const appRoutes: RouteObject[] = [
  {
    id: "app",
    path: "/",
    Component: App,
    ErrorBoundary: RouteError,
    HydrateFallback: AppLoading,
    children: [
      {
        index: true,
        id: "home",
        handle: {
          page: "home",
          title: "home",
          navigation: "home",
        },
        lazy: () =>
          import("./pages/home/Home").then(({ Home }) => ({ Component: Home })),
      },
      {
        path: "search",
        id: "search",
        handle: {
          page: "search",
          title: "search",
          navigation: "search",
        },
        lazy: () =>
          import("./pages/search/Search").then(({ Search }) => ({
            Component: Search,
          })),
      },
      {
        path: "spaces",
        id: "spaces",
        handle: {
          page: "spaces",
          title: "spaces",
          navigation: "spaces",
        },
        lazy: () =>
          import("./pages/spaces/Spaces").then(({ Spaces }) => ({
            Component: Spaces,
          })),
      },
      {
        path: "spaces/:spaceId",
        id: "space",
        handle: {
          page: "space",
          title: "spaces",
          navigation: "spaces",
        },
        lazy: () =>
          import("./pages/space-detail/SpaceDetail").then(
            ({ SpaceDetail }) => ({ Component: SpaceDetail }),
          ),
      },
      {
        path: "contexts/:knowledgeId",
        id: "context",
        handle: {
          page: "context",
          title: "contextDetailTitle",
          navigation: "search",
        },
        lazy: () =>
          import("./pages/context-detail/ContextDetail").then(
            ({ ContextDetail }) => ({ Component: ContextDetail }),
          ),
      },
      {
        path: "documents/:knowledgeId",
        id: "document",
        handle: {
          page: "document",
          title: "documentReaderTitle",
          navigation: "search",
        },
        lazy: () =>
          import("./pages/document-reader/DocumentReader").then(
            ({ DocumentReader }) => ({ Component: DocumentReader }),
          ),
      },
      {
        path: "settings",
        id: "settings",
        handle: { page: "settings", title: "settings", navigation: "settings" },
        lazy: () =>
          import("./pages/settings/Settings").then(({ Settings }) => ({
            Component: Settings,
          })),
      },
      {
        path: "*",
        id: "not-found",
        handle: { page: "home", title: "home", navigation: "home" },
        Component: RouteRedirect,
      },
    ] satisfies PageRoute[],
  },
];
