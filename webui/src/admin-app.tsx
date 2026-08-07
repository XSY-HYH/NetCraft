import { Suspense, lazy, useEffect, useState } from 'react';
import { Provider } from 'react-redux';
import { Route, Routes, useNavigate } from 'react-router-dom';

import PageBackground from '@/components/page_background';
import PageLoading from '@/components/page_loading';
import Toaster from '@/components/toaster';
import DialogProvider from '@/contexts/dialog';
import { tpgaApi } from '@/api/tpga';
import useAuth from '@/hooks/auth';
import useI18n from '@/hooks/use-i18n';
import store from '@/store';

const AdminLoginPage = lazy(() => import('@/pages/admin/login'));
const AdminPasswordPage = lazy(() => import('@/pages/admin/password'));
const AdminIndexPage = lazy(() => import('@/pages/admin/index'));
const OverviewPage = lazy(() => import('@/pages/admin/overview'));
const ApiAccountsPage = lazy(() => import('@/pages/admin/api_accounts'));
const PlayersPage = lazy(() => import('@/pages/admin/players'));
const AboutPage = lazy(() => import('@/pages/admin/about'));

//AdminApp 管理后台根组件 登录/改密独立路由 其余经 AuthChecker 校验 session
export default function AdminApp () {
  return (
    <DialogProvider>
      <Provider store={store}>
        <PageBackground />
        <Toaster />
        <Suspense fallback={<PageLoading />}>
          <Routes>
            <Route path='/login' element={<AdminLoginPage />} />
            <Route path='/password' element={<AdminPasswordPage />} />
            <Route path='/*' element={<AuthChecker><AdminRoutes /></AuthChecker>} />
          </Routes>
        </Suspense>
      </Provider>
    </DialogProvider>
  );
}

//AdminRoutes 已认证路由 DefaultLayout 包裹子页面
function AdminRoutes () {
  return (
    <Routes>
      <Route path='/' element={<AdminIndexPage />}>
        <Route index element={<OverviewPage />} />
        <Route path='api-accounts' element={<ApiAccountsPage />} />
        <Route path='players' element={<PlayersPage />} />
        <Route path='password' element={<AdminPasswordPage />} />
        <Route path='about' element={<AboutPage />} />
      </Route>
    </Routes>
  );
}

//AuthChecker 调 getSession 校验 cookie session must_change 跳改密 失效跳登录
//登录成功后用管理员持久化语言覆盖浏览器语言 非空则加载对应翻译
function AuthChecker ({ children }: { children: React.ReactNode; }) {
  const { setAuth, revokeAuth } = useAuth();
  const { applySessionLanguage } = useI18n();
  const navigate = useNavigate();
  const [state, setState] = useState<'checking' | 'ok' | 'redirect'>('checking');

  useEffect(() => {
    tpgaApi.getSession()
      .then(async (s) => {
        if (s.ok) {
          setAuth(true);
          if (s.mustChangePassword) {
            navigate('/password', { replace: true });
            setState('redirect');
            return;
          }
          //管理员持久化语言优先于浏览器语言
          await applySessionLanguage(s.language);
          setState('ok');
        } else {
          revokeAuth();
          navigate('/login', { replace: true });
          setState('redirect');
        }
      })
      .catch(() => {
        revokeAuth();
        navigate('/login', { replace: true });
        setState('redirect');
      });
  }, []);

  if (state !== 'ok') return <PageLoading />;
  return <>{children}</>;
}
