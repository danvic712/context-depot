import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";
import { defineConfig } from "vite";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath } from "node:url";

// https://vite.dev/config/
export default defineConfig({
  resolve: {
    tsconfigPaths: true,
  },
  plugins: [
    react(),
    tailwindcss(),
    babel({ presets: [reactCompilerPreset()] }),
  ],
  build: {
    outDir: "../ContextDepot/wwwroot",
    emptyOutDir: true,
  },
  server: {
    host: "localhost",
    port: 5173,
    strictPort: true,
    fs: {
      allow: [
        fileURLToPath(new URL(".", import.meta.url)),
        fileURLToPath(new URL("../../locales", import.meta.url)),
      ],
    },
    proxy: {
      "/api": "http://localhost:5289",
      "/healthz": "http://localhost:5289",
      "/readyz": "http://localhost:5289",
    },
  },
});
