import { useCallback, useState } from 'react';

//玩家认证 cookie session 后端下发 HttpOnly cookie 前端不持有 token
//本地仅存登录标记 用于路由守卫快速判断 实际有效性由 getSession 校验
//与 admin auth 独立 避免共享 localStorage key 互相覆盖
const AUTH_KEY = 'tpga_player_authed';

const usePlayerAuth = () => {
  const [authed, setAuthed] = useState<boolean>(() => localStorage.getItem(AUTH_KEY) === '1');

  const setAuth = useCallback((v: boolean) => {
    setAuthed(v);
    if (v) localStorage.setItem(AUTH_KEY, '1');
    else localStorage.removeItem(AUTH_KEY);
  }, []);

  const revokeAuth = useCallback(() => {
    setAuthed(false);
    localStorage.removeItem(AUTH_KEY);
  }, []);

  return { isAuth: authed, setAuth, revokeAuth };
};

export default usePlayerAuth;
