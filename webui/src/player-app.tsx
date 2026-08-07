import { Suspense, lazy } from 'react';
import { Provider } from 'react-redux';
import { Navigate, Route, Routes } from 'react-router-dom';

import PageBackground from '@/components/page_background';
import PageLoading from '@/components/page_loading';
import Toaster from '@/components/toaster';
import DialogProvider from '@/contexts/dialog';
import store from '@/store';

const PlayerInfoPage = lazy(() => import('@/pages/player/info'));
const PlayerHomePage = lazy(() => import('@/pages/player/Home'));
const PlayerProfilePage = lazy(() => import('@/pages/player/Profile'));
const PlayerPasswordPage = lazy(() => import('@/pages/player/Password'));
const PlayerLoginPage = lazy(() => import('@/pages/player/Login'));
const PlayerRegisterPage = lazy(() => import('@/pages/player/Register'));
const ComingSoonPage = lazy(() => import('@/pages/player/ComingSoon'));

//PlayerApp 玩家端根组件
//首页需登录的个人中心 /about 公共服务说明 /login 登录 其余占位由后续阶段替换
//阶段2: Home/Profile/Password 落地 阶段3+: Closet/Sessions/History 逐步替换
export default function PlayerApp () {
  return (
    <DialogProvider>
      <Provider store={store}>
        <PageBackground />
        <Toaster />
        <Suspense fallback={<PageLoading />}>
          <Routes>
            {/* 登录后个人中心 Home 自带 session 守卫 */}
            <Route path='/' element={<PlayerHomePage />} />
            {/* 公共服务说明 无需登录 */}
            <Route path='/about' element={<PlayerInfoPage />} />
            {/* 玩家登录 */}
            <Route path='/login' element={<PlayerLoginPage />} />
            {/* 阶段2 已落地 页面均自带 PlayerLayout 守卫 */}
            <Route path='/profile' element={<PlayerProfilePage />} />
            <Route path='/password' element={<PlayerPasswordPage />} />
            {/* 暂未实现的功能 占位页 阶段3+ 逐步替换 */}
            {/* OAuth 补全注册 OAuth 回调未绑定时跳此 */}
            <Route path='/register' element={<PlayerRegisterPage />} />
            <Route path='/auth/forgot' element={<ComingSoonPage />} />
            <Route path='/verify-email' element={<ComingSoonPage />} />
            <Route path='/user/closet' element={<ComingSoonPage />} />
            <Route path='/sessions' element={<ComingSoonPage />} />
            <Route path='/history' element={<ComingSoonPage />} />
            <Route path='/u/:username' element={<ComingSoonPage />} />
            {/* 其他路径兜底到首页 */}
            <Route path='*' element={<Navigate to='/' replace />} />
          </Routes>
        </Suspense>
      </Provider>
    </DialogProvider>
  );
}
