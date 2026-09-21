import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    // Le backend tourne dans Docker (publié sur 5000). On proxifie /api comme le
    // fait nginx en prod : le navigateur ne voit qu'une seule origine, donc pas
    // de preflight CORS. Laisser VITE_API_URL vide pour que le code retombe
    // sur "/api" (cf. front/src/api/axios.js).
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.{js,ts}"],
    coverage: {
      provider: "v8",
      reporter: ["text", "html"],
    },
  },
});
