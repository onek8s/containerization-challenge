import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// `npm run dev` serves the app on 5173 and forwards API, hub and sign-in calls to the
// portal host (dotnet run, http://localhost:5320).
const portal = 'http://localhost:5320';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
    sourcemap: false
  },
  server: {
    port: 5173,
    proxy: {
      '/api': portal,
      '/bff': portal,
      '/signin-oidc': portal,
      '/hubs': { target: portal, ws: true }
    }
  }
});
