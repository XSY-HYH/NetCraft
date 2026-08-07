import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';
import { ViteImageOptimizer } from 'vite-plugin-image-optimizer';
import tsconfigPaths from 'vite-tsconfig-paths';

//多入口 admin.html 管理后台 player.html 玩家端 共享 assets 与依赖分包
//base '/' 后端 / 返回 admin.html /player 返回 player.html 资源走 /assets
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd());
  const backendDebugUrl = env.VITE_DEBUG_BACKEND_URL;
  return {
    plugins: [
      react(),
      tsconfigPaths(),
      ViteImageOptimizer({}),
    ],
    base: '/',
    server: {
      proxy: {
        '/api': backendDebugUrl,
        '/i18n': backendDebugUrl,
        '/login': backendDebugUrl,
        '/logout': backendDebugUrl,
        '/password': backendDebugUrl,
        '/ws': { target: backendDebugUrl, ws: true, changeOrigin: true },
      },
    },
    build: {
      assetsInlineLimit: 0,
      rollupOptions: {
        input: {
          admin: 'admin.html',
          player: 'player.html',
        },
        output: {
          //分离 react 避免混入 heroui chunk 导致 Activity undefined 报错
          manualChunks (id) {
            if (id.includes('node_modules')) {
              if (/[\\/]node_modules[\\/]react[\\/]/.test(id)) {
                return 'react';
              }
              if (id.includes('react-dom')) {
                return 'react-dom';
              }
              if (id.includes('react-router-dom')) {
                return 'react-router-dom';
              }
              if (id.includes('@heroui/')) {
                return 'heroui';
              }
              if (id.includes('react-hook-form')) {
                return 'react-hook-form';
              }
              if (id.includes('react-hot-toast')) {
                return 'react-hot-toast';
              }
              if (id.includes('@reduxjs') || id.includes('react-redux')) {
                return 'redux';
              }
              if (id.includes('motion')) {
                return 'motion';
              }
              if (id.includes('react-icons')) {
                return 'react-icons';
              }
            }
          },
        },
      },
    },
  };
});
