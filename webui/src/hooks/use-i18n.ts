import { useCallback, useEffect, useState } from 'react';
import { tpgaApi } from '@/api/tpga';

//detectBrowserLang 按 navigator.language 映射到 webui 语言代码 无匹配返回 undefined 由后端回退
function detectBrowserLang (): string | undefined {
  if (typeof navigator === 'undefined') return undefined;
  const lang = (navigator.language || '').toLowerCase();
  if (lang.startsWith('zh')) return 'zh_CN';
  if (lang.startsWith('en')) return 'en_US';
  return undefined;
}

//缓存避免多组件重复请求 同会话只拉一次
let cachedTranslations: Record<string, string> = {};
let cachedLang = '';
//请求 token 丢弃旧请求结果 避免 useEffect 与 AuthChecker 并发加载竞态
let loadToken = 0;
//switchLanguage 暂存目标语言 reload 后优先用它避免依赖 session 时序
const PENDING_LANG_KEY = 'tpga_pending_lang';

const useI18n = () => {
  const [translations, setTranslations] = useState<Record<string, string>>(cachedTranslations);
  const [lang, setLang] = useState(cachedLang);

  const loadTranslations = useCallback(async (forceLang?: string) => {
    const myToken = ++loadToken;
    try {
      //优先使用显式传入语言 其次浏览器语言 后端按存在性回退 config.Language
      const requestLang = forceLang ?? detectBrowserLang();
      const data = await tpgaApi.getTranslations(requestLang);
      //旧请求后返回 丢弃 避免竞态覆盖最新语言
      if (myToken !== loadToken) return;
      if (data?.translations) {
        cachedTranslations = data.translations;
        cachedLang = data.lang;
        setTranslations(data.translations);
        setLang(data.lang);
      }
    } catch (e) {
      console.error('Failed to load translations:', e);
    }
  }, []);

  useEffect(() => {
    if (Object.keys(cachedTranslations).length === 0) {
      //switchLanguage 暂存的目标语言优先 避免浏览器语言与 session 加载竞态
      const pending = sessionStorage.getItem(PENDING_LANG_KEY);
      if (pending) {
        sessionStorage.removeItem(PENDING_LANG_KEY);
        loadTranslations(pending);
      } else {
        loadTranslations();
      }
    }
  }, [loadTranslations]);

  //applySessionLanguage 登录后用管理员持久化语言覆盖浏览器语言 非空且与缓存不同则重载
  const applySessionLanguage = useCallback(async (sessionLang?: string | null) => {
    if (!sessionLang) return;
    if (sessionLang === cachedLang) return;
    await loadTranslations(sessionLang);
  }, [loadTranslations]);

  //switchLanguage 切换语言 持久化到管理员字段后重载页面让所有组件应用新语言
  const switchLanguage = useCallback(async (target: string) => {
    try {
      await tpgaApi.setLanguage(target);
      //reload 后 useEffect 优先用 target 加载 避免浏览器语言竞态覆盖
      sessionStorage.setItem(PENDING_LANG_KEY, target);
      cachedTranslations = {};
      cachedLang = '';
      //重载页面 重新初始化所有组件加载新语言
      window.location.reload();
    } catch (e) {
      console.error('Failed to switch language:', e);
    }
  }, []);

  const t = useCallback((key: string, ...args: (string | number)[]): string => {
    let text = translations[key] || key;
    args.forEach((v, i) => {
      text = text.replace(new RegExp(`\\{${i}\\}`, 'g'), String(v));
    });
    return text;
  }, [translations]);

  //tError 错误码翻译 code 映射 webui.error.{code} 无翻译回退 fallback
  const tError = useCallback((code: string | undefined, fallback: string): string => {
    if (!code) return fallback;
    const key = `webui.error.${code}`;
    return translations[key] || fallback;
  }, [translations]);

  return { t, tError, lang, translations, loadTranslations, applySessionLanguage, switchLanguage };
};

export default useI18n;
