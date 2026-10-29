import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

// Served by ControlTower.Tower.Api under /app/; the API and hub are proxied in "npm run dev".
export default defineConfig({
  base: '/app/',
  plugins: [vue()],
  build: {
    outDir: '../wwwroot/app',
    emptyOutDir: true,
    sourcemap: false,
    chunkSizeWarningLimit: 900,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        silent: resolve(__dirname, 'silent-renew.html')
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5180',
      '/hubs': { target: 'http://localhost:5180', ws: true }
    }
  }
})
