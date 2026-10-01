import { AppLoading } from "./components/feedback/RouteFeedback";
import { StrictMode } from "react";
import { ThemeProvider } from "next-themes";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import { createRoot } from "react-dom/client";
import "./styles/app.css";
import "./styles/page-transitions.css";
import { appRoutes } from "./routes";
import { initializeI18n, type Lang } from "./lib/i18n";
import { Toaster } from "./components/ui/sonner";
import {
  getAppearance,
  getInitialAppearance,
  toUiLanguage,
} from "./features/settings/appearance-api";
import {
  load,
  save,
  type Theme,
} from "./features/settings/browser-preferences";

const router = createBrowserRouter(appRoutes);

const root = createRoot(document.getElementById("root")!);
root.render(<AppLoading pathname={router.state.location.pathname} />);

getAppearance()
  .catch(() => getInitialAppearance().settings)
  .then((settings) =>
    initializeI18n(
      load<Lang>("contextdepot.language", ["en", "zh"]) ??
        toUiLanguage(settings.language),
    ),
  )
  .then(() => {
    const theme =
      load<Theme>("contextdepot.theme", ["system", "light", "dark"]) ??
      getInitialAppearance().settings.theme;
    save("contextdepot.theme-mode", theme);
    root.render(
      <StrictMode>
        <ThemeProvider
          attribute="data-theme"
          storageKey="contextdepot.theme-mode"
          defaultTheme={theme}
          enableSystem
          disableTransitionOnChange
        >
          <RouterProvider router={router} />
          <Toaster closeButton />
        </ThemeProvider>
      </StrictMode>,
    );
  })
  .catch(() => {
    root.render(
      <p role="alert">
        Unable to load translations. Please reload. /
        无法加载语言资源，请刷新页面。
      </p>,
    );
  });
