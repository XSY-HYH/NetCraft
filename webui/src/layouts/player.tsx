import { Button } from '@heroui/button';
import { Dropdown, DropdownItem, DropdownMenu, DropdownTrigger } from '@heroui/dropdown';
import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { IoLogOutOutline, IoSettingsOutline } from 'react-icons/io5';
import { LuLanguages, LuMoon, LuSun } from 'react-icons/lu';

import { playerApi } from '@/api/player';
import usePlayerAuth from '@/hooks/player-auth';
import useI18n from '@/hooks/use-i18n';
import { useTheme } from '@/hooks/use-theme';

//PlayerLayout 玩家端布局 顶部导航栏 + 路由守卫
//useEffect 调 /api/player/session 校验 未登录跳 /login?return=原路径
//session 校验通过渲染 children 期间返回 null 避免闪烁
//导航栏右侧设置 Dropdown 含语言切换 + 主题切换 偏好持久化到数据库
export default function PlayerLayout ({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { t, lang, translations, applySessionLanguage, switchLanguage } = useI18n();
  const { isDark, toggle, applySessionTheme } = useTheme();
  const { revokeAuth } = usePlayerAuth();
  const [username, setUsername] = useState<string | null>(null);
  //可用语言列表
  const [languages, setLanguages] = useState<string[]>([]);

  useEffect(() => {
    playerApi.getSession()
      .then((s) => {
        if (!s.ok) {
          revokeAuth();
          navigate('/login?return=' + encodeURIComponent(location.pathname), { replace: true });
        } else {
          setUsername(s.username ?? '');
          //数据库偏好覆盖浏览器/系统默认
          if (s.language) applySessionLanguage(s.language);
          if (s.theme) applySessionTheme(s.theme);
        }
      })
      .catch(() => {
        revokeAuth();
        navigate('/login?return=' + encodeURIComponent(location.pathname), { replace: true });
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  //加载语言列表
  useEffect(() => {
    import('@/api/tpga').then(({ tpgaApi }) =>
      tpgaApi.getLanguages().then((r) => setLanguages(r.languages)).catch(() => {})
    );
  }, []);

  //langLabel 语言代码转本地化名称
  const langLabel = (code: string) => translations[`webui.lang.${code}`] || code;

  //切换语言 持久化到数据库后重载
  const onSwitchLanguage = async (target: string) => {
    try {
      await playerApi.updatePreferences(target, null);
      sessionStorage.setItem('tpga_pending_lang', target);
      window.location.reload();
    } catch { /* 忽略 */ }
  };

  //切换主题 即时生效 + 异步持久化
  const onToggleTheme = async () => {
    toggle();
    try {
      await playerApi.updatePreferences(null, isDark ? 'light' : 'dark');
    } catch { /* 忽略 */ }
  };

  const onLogout = async () => {
    try { await playerApi.logout(); } catch { /* 忽略 */ }
    revokeAuth();
    navigate('/login', { replace: true });
  };

  //session 校验期间不渲染 避免未登录内容闪烁
  if (username === null) return null;

  return (
    <div className='min-h-screen flex flex-col'>
      <header className='flex items-center justify-between px-4 md:px-6 py-3 border-b border-default-200 dark:border-default-100 backdrop-blur-lg bg-background/50 sticky top-0 z-30'>
        <div className='flex items-center gap-2 cursor-pointer' onClick={() => navigate('/')}>
          <span className='text-lg font-bold'>NetCraft</span>
          <span className='text-small text-default-400 hidden sm:inline'>{t('webui.app.player')}</span>
        </div>
        <div className='flex items-center gap-1 md:gap-2'>
          <Button size='sm' variant='light' onPress={() => navigate('/')}>{t('webui.player.nav.home')}</Button>
          <Button size='sm' variant='light' onPress={() => navigate('/profile')}>{t('webui.player.nav.profile')}</Button>
          <Button size='sm' variant='light' onPress={() => navigate('/password')}>{t('webui.player.nav.password')}</Button>
          {/*设置 Dropdown 语言+主题切换*/}
          <Dropdown placement='bottom-end'>
            <DropdownTrigger>
              <Button isIconOnly size='sm' variant='light'>
                <IoSettingsOutline className='text-lg' />
              </Button>
            </DropdownTrigger>
            <DropdownMenu aria-label='player settings' selectionMode='single' selectedKeys={lang ? [lang] : []}>
              {languages.map((l) => (
                <DropdownItem key={l} startContent={<LuLanguages size={16} />} onPress={() => onSwitchLanguage(l)}>
                  {langLabel(l)}
                </DropdownItem>
              ))}
              <DropdownItem key='divider' className='h-px my-1 bg-default-200 dark:bg-default-100' isReadOnly />
              <DropdownItem
                key='theme'
                startContent={isDark ? <LuSun size={16} /> : <LuMoon size={16} />}
                onPress={onToggleTheme}
              >
                {isDark ? t('webui.theme.light') : t('webui.theme.dark')}
              </DropdownItem>
            </DropdownMenu>
          </Dropdown>
          <span className='text-small text-default-500 hidden md:inline mx-2'>{username}</span>
          <Button size='sm' color='danger' variant='flat' startContent={<IoLogOutOutline />} onPress={onLogout}>
            {t('webui.player.profile.logout')}
          </Button>
        </div>
      </header>
      <main className='flex-1 overflow-y-auto'>
        {children}
      </main>
    </div>
  );
}
