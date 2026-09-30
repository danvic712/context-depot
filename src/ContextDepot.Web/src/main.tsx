import { AppLoading } from "./components/feedback/RouteFeedback";
import { StrictMode } from "react";
import { ThemeProvider } from "next-themes";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import { createRoot } from "react-dom/client";
import "./styles/app.css";
import { appRoutes } from "./routes";
import { initializeI18n } from "./lib/i18n";
import { Toaster } from "./components/ui/sonner";
import { load, type Theme } from "./features/settings/browser-preferences";

const router = createBrowserRouter(appRoutes);

const root = createRoot(document.getElementById("root")!);
root.render(<AppLoading />);

initializeI18n()
  .then(() => {
    root.render(
      <StrictMode>
        <ThemeProvider
          attribute="data-theme"
          storageKey="contextdepot.theme-mode"
          defaultTheme={
            load<Theme>("contextdepot.theme", ["system", "light", "dark"]) ??
            "system"
          }
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
