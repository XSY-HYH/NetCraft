import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import AdminApp from '@/admin-app';
import { Provider } from '@/provider';
import { initTheme } from '@/hooks/use-theme';
import '@/styles/globals.css';

//admin 入口 根 / 与 /admin 服务此 html 主题按浏览器动态设定
initTheme();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter basename='/'>
    <Provider>
      <AdminApp />
    </Provider>
  </BrowserRouter>
);
