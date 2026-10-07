import { lazyPage } from "@/lib/lazy-page";
import type { RouteObject } from "react-router";
import App from "./App";
import {
  AppLoading,
  PageRouteError,
  RouteError,
} from "@/components/feedback/RouteFeedback";
import { PageNotFound } from "@/components/feedback/PageState";

import type { PageHandle } from "@/hooks/use-app-context";
import { setupLoader } from "@/features/setup/setup-loader";

type PageRoute = RouteObject & { handle: PageHandle };

export const appRoutes: RouteObject[] = [
  {
    id: "app",
    path: "/",
    Component: App,
    loader: setupLoader,
    shouldRevalidate: ({ currentUrl, nextUrl }) =>
      currentUrl.pathname !== nextUrl.pathname ||
      currentUrl.search === nextUrl.search,
    ErrorBoundary: RouteError,
    HydrateFallback: AppLoading,
    children: [
      {
        path: "setup",
        id: "setup",
        ErrorBoundary: PageRouteError,
        handle: { page: "setup", title: "setupTitle", navigation: "home" },
        lazy: lazyPage(() =>
          import("./pages/setup/Setup").then(({ Setup }) => ({
            Component: Setup,
          })),
        ),
      },
      {
        index: true,
        id: "home",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "home",
          title: "home",
          navigation: "home",
        },
        lazy: lazyPage(() =>
          import("./pages/home/Home").then(({ Home }) => ({ Component: Home })),
        ),
      },
      {
        path: "search",
        id: "search",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "search",
          title: "search",
          navigation: "search",
        },
        lazy: lazyPage(() =>
          import("./pages/search/Search").then(({ Search }) => ({
            Component: Search,
          })),
        ),
      },
      {
        path: "spaces",
        id: "spaces",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "spaces",
          title: "spaces",
          navigation: "spaces",
        },
        lazy: lazyPage(() =>
          import("./pages/spaces/Spaces").then(({ Spaces }) => ({
            Component: Spaces,
          })),
        ),
      },
      {
        path: "spaces/:spaceId",
        id: "space",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "space",
          title: "spaces",
          navigation: "spaces",
        },
        lazy: lazyPage(() =>
          import("./pages/space-detail/SpaceDetail").then(
            ({ SpaceDetail }) => ({ Component: SpaceDetail }),
          ),
        ),
      },
      {
        path: "contexts/:knowledgeId",
        id: "context",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "context",
          title: "contextDetailTitle",
          navigation: "search",
        },
        lazy: lazyPage(() =>
          import("./pages/context-detail/ContextDetail").then(
            ({ ContextDetail }) => ({ Component: ContextDetail }),
          ),
        ),
      },
      {
        path: "documents/:knowledgeId",
        id: "document",
        ErrorBoundary: PageRouteError,
        handle: {
          page: "document",
          title: "documentReaderTitle",
          navigation: "search",
        },
        lazy: lazyPage(() =>
          import("./pages/document-reader/DocumentReader").then(
            ({ DocumentReader }) => ({ Component: DocumentReader }),
          ),
        ),
      },
      {
        path: "settings",
        id: "settings",
        ErrorBoundary: PageRouteError,
        handle: { page: "settings", title: "settings", navigation: "settings" },
        lazy: lazyPage(() =>
          import("./pages/settings/Settings").then(({ Settings }) => ({
            Component: Settings,
          })),
        ),
      },
      {
        path: "404",
        id: "not-found-page",
        ErrorBoundary: PageRouteError,
        handle: { page: "notFound", title: "pageNotFound", navigation: "home" },
        Component: PageNotFound,
      },
      {
        path: "*",
        id: "not-found",
        ErrorBoundary: PageRouteError,
        handle: { page: "notFound", title: "pageNotFound", navigation: "home" },
        Component: PageNotFound,
      },
    ] satisfies PageRoute[],
  },
];
