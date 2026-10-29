import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue2'

export default defineConfig({
  plugins: [vue()],
  build: { outDir: '../wwwroot', emptyOutDir: true },
  server: { proxy: { '/api': 'http://localhost:5104' } }
})
