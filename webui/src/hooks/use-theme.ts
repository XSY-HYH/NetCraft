import { useCallback, useEffect, useState } from 'react';

//主题管理 dark/light 根据 prefers-color-scheme 动态设定 手动切换后 localStorage 持久化
//localStorage key=theme 值用 JSON.stringify 保持与原版 useLocalStorage 格式一致
type Theme = 'light' | 'dark';

const STORAGE_KEY = 'theme';

//getSystemTheme 读浏览器 prefers-color-scheme 无 matchMedia 默认 dark
function getSystemTheme (): Theme {
  if (typeof window === 'undefined' || !window.matchMedia) return 'dark';
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

//parseTheme 从 localStorage 解析主题 无值或非法返回 null 表示未手动选过
function parseTheme (raw: string | null): Theme | null {
  if (!raw) return null;
  try {
    const v = JSON.parse(raw);
    if (v === 'light' || v === 'dark') return v;
  } catch {
    //原版可能存裸字符串 兼容
    if (raw === 'light' || raw === 'dark') return raw;
  }
  return null;
}

//getInitialTheme localStorage 有手动选择用之 否则跟系统
function getInitialTheme (): Theme {
  const saved = parseTheme(localStorage.getItem(STORAGE_KEY));
  return saved ?? getSystemTheme();
}

//applyTheme 设 html class 与 localStorage 持久化
function applyTheme (theme: Theme) {
  const root = document.documentElement;
  root.classList.remove('light', 'dark');
  root.classList.add(theme);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(theme));
}

//initTheme 首屏同步设 class 避免 hook 异步 applyTheme 导致闪烁 main 入口调用
export function initTheme () {
  applyTheme(getInitialTheme());
}

export const useTheme = () => {
  const [theme, setThemeState] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  //系统主题变化时 若用户未手动选择(localStorage 无值)则跟随系统
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: light)');
    const handler = (e: MediaQueryListEvent) => {
      if (parseTheme(localStorage.getItem(STORAGE_KEY)) === null) {
        setThemeState(e.matches ? 'light' : 'dark');
      }
    };
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  }, []);

  const setTheme = useCallback((t: Theme) => setThemeState(t), []);
  const toggle = useCallback(() => setThemeState((t) => (t === 'dark' ? 'light' : 'dark')), []);
  //setDarkTheme 保留兼容原版接口
  const setDarkTheme = useCallback(() => setThemeState('dark'), []);

  //applySessionTheme 登录后用数据库持久化主题覆盖当前值 类似 useI18n.applySessionLanguage
  const applySessionTheme = useCallback((sessionTheme?: string | null) => {
    if (!sessionTheme) return;
    if (sessionTheme === 'light' || sessionTheme === 'dark') {
      setThemeState(sessionTheme);
    }
  }, []);

  return { theme, isDark: theme === 'dark', isLight: theme === 'light', setTheme, toggle, setDarkTheme, applySessionTheme };
};
