import { AppLoading } from "./components/feedback/RouteFeedback";
import { StrictMode } from "react";
import { ThemeProvider } from "next-themes";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import { createRoot } from "react-dom/client";
import "./styles/app.css";
import "./styles/page-transitions.css";
import { appRoutes } from "./routes";
import { initializeI18n } from "./lib/i18n";
import { Toaster } from "./components/ui/sonner";
import {
  getAppearance,
  getInitialAppearance,
} from "./features/settings/appearance-api";
import { save } from "./features/settings/browser-preferences";
import { resolveAppearancePreferences } from "./features/settings/appearance-preferences";
import type { SetupGate } from "./features/setup/setup-loader";

const router = createBrowserRouter(appRoutes);
const setupMode = new Promise<boolean>((resolve) => {
  const resolveMode = () => {
    const gate = router.state.loaderData.app as SetupGate | undefined;
    resolve(gate?.status?.state !== "completed");
  };
  if (router.state.initialized) resolveMode();
  else {
    const unsubscribe = router.subscribe((state) => {
      if (!state.initialized) return;
      unsubscribe();
      resolveMode();
    });
  }
});

const root = createRoot(document.getElementById("root")!);
root.render(<AppLoading pathname={router.state.location.pathname} />);

Promise.all([
  getAppearance().catch(() => getInitialAppearance().settings),
  setupMode,
])
  .then(async ([settings, setup]) => {
    const { theme, language } = resolveAppearancePreferences(settings, setup);
    await initializeI18n(language);
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
