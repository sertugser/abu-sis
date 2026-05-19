import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5213',
        changeOrigin: true,
        secure: false,
      },
      // SignalR WebSocket bağlantısı — ws: true olmadan vite dev sunucusu
      // upgrade isteğini forward etmez, frontend "connection refused" görür.
      '/hubs': {
        target: 'http://127.0.0.1:5213',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
})
