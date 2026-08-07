import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import PlayerApp from '@/player-app';
import { Provider } from '@/provider';
import { initTheme } from '@/hooks/use-theme';
import '@/styles/globals.css';

//player 入口 主端口根 / 服务此 html 玩家端公共信息
initTheme();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter basename='/'>
    <Provider>
      <PlayerApp />
    </Provider>
  </BrowserRouter>
);
