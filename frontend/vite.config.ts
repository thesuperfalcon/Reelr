import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// The backend runs on the "http" launch profile. Proxying avoids CORS in development.
const backendUrl = "http://localhost:5084";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      "/api": backendUrl,
      "/auth": backendUrl,
    },
  },
});
