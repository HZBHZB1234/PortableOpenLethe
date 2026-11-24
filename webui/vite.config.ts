import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath } from 'node:url'

// Two static SPAs share one hashed asset graph. Absolute base is required because
// AuthEndpoints serves auth/index.html at /auth/login (not just /auth/), and
// relative ./assets/ URLs would resolve against /auth/ instead of the site root.
const entry = (p: string) => fileURLToPath(new URL(p, import.meta.url))

export default defineConfig({
  base: '/',
  plugins: [vue()],
  build: {
    // webui/ is at the repository root: one level up is PortableOpenLethe/.
    outDir: '../src/OpenLethe.Server/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      input: {
        admin: entry('admin/index.html'),
        auth: entry('auth/index.html'),
      },
    },
  },
})
