import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";
import { defineConfig, loadEnv } from "vite";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath } from "node:url";
import { resolveBuildVersion } from "../../scripts/release-version.mts";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, fileURLToPath(new URL(".", import.meta.url)), "");
  if (env.RELEASE_BUILD && !["true", "false"].includes(env.RELEASE_BUILD))
    throw new Error("RELEASE_BUILD must be true or false.");
  const version = resolveBuildVersion(
    env.VITE_APP_VERSION,
    env.RELEASE_BUILD === "true",
  );
  return {
    resolve: {
      tsconfigPaths: true,
    },
    plugins: [
      {
        name: "context-depot-version",
        generateBundle() {
          this.emitFile({
            type: "asset",
            fileName: "version.json",
            source:
              JSON.stringify({
                version: version.appVersion,
                channel: version.channel,
              }) + "\n",
          });
        },
      },
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
        "/mcp": "http://localhost:5289",
      },
    },
  };
});
