import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Geliştirmede API aynı adresten çağrılır; böylece CORS ayarı gerekmez. 5080, api'nin launchSettings portudur.
    proxy: {
      '/api': 'http://localhost:5080',
      // SignalR (A4, /hubs/review); WebSocket de yönlendiriliyor.
      '/hubs': { target: 'http://localhost:5080', ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
