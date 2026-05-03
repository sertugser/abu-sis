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
        // HTTPS API'ye direkt git — UseHttpsRedirection() ile HTTP→HTTPS 307 redirect
        // tarayıcıya leak ediyordu (cert authority invalid). HTTPS direkt + secure:false
        // ile dev cert kabul edilir, tarayıcı sadece localhost:5173 görür.
        target: 'https://127.0.0.1:7035',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
