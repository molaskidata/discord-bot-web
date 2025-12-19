import {defineConfig} from 'vite';

export default defineConfig({
  envDir: '../',
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: 'http://localhost:3002',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
    hmr: {
      clientPort: 443,
    },
  },
});
