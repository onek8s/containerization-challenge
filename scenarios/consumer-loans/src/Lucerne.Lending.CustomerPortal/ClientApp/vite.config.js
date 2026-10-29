import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue2'

// built into ../wwwroot/app and served by the portal host; `npm run dev` proxies the host on :5101
export default defineConfig({
  base: '/app/',
  plugins: [vue()],
  build: {
    outDir: '../wwwroot/app',
    emptyOutDir: true,
    chunkSizeWarningLimit: 900
  },
  server: {
    port: 5173,
    proxy: {
      '/portal-api': 'http://localhost:5101',
      '/account': 'http://localhost:5101'
    }
  }
})
