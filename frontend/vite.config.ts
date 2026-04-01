import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// Resolve paths from this file so .env and index.html load correctly even when
// `vite` is started with a different current working directory (e.g. IDE / monorepo root).
const frontendDir = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, frontendDir, "VITE_");
  if (mode === "production" && !env.VITE_MSAL_CLIENT_ID?.trim()) {
    throw new Error(
      "[vite] VITE_MSAL_CLIENT_ID is empty at build time. " +
        "Ensure frontend/.env exists and is not excluded from the Docker build context (frontend/.dockerignore), " +
        "or set VITE_* as Docker build-args / compose env. Then rebuild with --no-cache.",
    );
  }

  return {
    root: frontendDir,
    envDir: frontendDir,
    envPrefix: "VITE_",
    plugins: [react()],
    resolve: {
      alias: {
        "@": path.resolve(frontendDir, "src"),
      },
    },
    server: {
      port: 3000,
      host: true,
    },
    preview: {
      port: 3000,
      host: true,
    },
  };
});
