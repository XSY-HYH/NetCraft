//TPGA API 封装 基于 fetch cookie session 自动携带 credentials include
//响应 { ok, ... } 格式 ok=false 或非 2xx 抛 TpgaError 含 status 与 error 字段
//401 未登录 403 must_change_password 需改密 由调用方处理跳转

export class TpgaError extends Error {
  constructor (message: string, public status: number, public error?: string) {
    super(message);
    this.name = 'TpgaError';
  }
}

//http 通用请求封装 返回解析后的 JSON 非 ok 抛 TpgaError
async function http<T = any> (method: string, path: string, body?: unknown): Promise<T> {
  const init: RequestInit = { method, credentials: 'include' };
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(body);
  }
  init.headers = headers;
  const res = await fetch(path, init);
  const text = await res.text();
  let data: any = {};
  if (text) {
    try { data = JSON.parse(text); } catch { data = { message: text }; }
  }
  if (!res.ok || data.ok === false) {
    throw new TpgaError(data.message || data.error || `HTTP ${res.status}`, res.status, data.error);
  }
  return data as T;
}

//httpBinary 上传二进制 body 返回 JSON 皮肤上传用 非 ok 抛 TpgaError
async function httpBinary (method: string, path: string, body: BufferSource, contentType = 'application/octet-stream'): Promise<any> {
  const init: RequestInit = { method, credentials: 'include', body, headers: { 'Content-Type': contentType } };
  const res = await fetch(path, init);
  const text = await res.text();
  let data: any = {};
  if (text) { try { data = JSON.parse(text); } catch { data = { message: text }; } }
  if (!res.ok || data.ok === false) {
    throw new TpgaError(data.message || data.error || `HTTP ${res.status}`, res.status, data.error);
  }
  return data;
}

export interface SessionInfo { ok: boolean; username?: string; mustChangePassword?: boolean; language?: string | null; }
export interface ApiAccount { id: number; username: string; enabled: boolean; createdAt: string; }
export interface Player { id: number; uuid: string; username: string; enabled: boolean; email?: string | null; }
export interface TranslationsData { ok: boolean; lang: string; translations: Record<string, string>; }
export interface LanguagesData { ok: boolean; languages: string[]; }

export const tpgaApi = {
  //session 检查与登录登出改密
  getSession: () => http<SessionInfo>('GET', '/api/session'),
  login: (username: string, password: string) =>
    http<{ ok: boolean; mustChangePassword: boolean }>('POST', '/login', { username, password }),
  logout: () => http<{ ok: boolean }>('POST', '/logout'),
  changePassword: (oldPassword: string, newPassword: string) =>
    http<{ ok: boolean }>('POST', '/password', { oldPassword, newPassword }),
  //管理员语言偏好持久化到 admin_accounts.language
  setLanguage: (language: string) =>
    http<{ ok: boolean; language: string }>('POST', '/api/language', { language }),

  //api 账户管理 wss 握手账户
  listApiAccounts: () => http<{ ok: boolean; accounts: ApiAccount[] }>('GET', '/api/accounts'),
  addApiAccount: (username: string, password: string) =>
    http<{ ok: boolean; id: number }>('POST', '/api/accounts', { username, password }),
  deleteApiAccount: (id: number) => http<{ ok: boolean }>('DELETE', `/api/accounts/${id}`),
  changeApiPassword: (id: number, newPassword: string) =>
    http<{ ok: boolean }>('POST', `/api/accounts/${id}/password`, { newPassword }),
  toggleApiEnabled: (id: number, enabled: boolean) =>
    http<{ ok: boolean; enabled: boolean }>('POST', `/api/accounts/${id}/enabled`, { enabled }),

  //游戏玩家管理 Yggdrasil 验证的游戏账户
  listPlayers: () => http<{ ok: boolean; players: Player[] }>('GET', '/api/players'),
  addPlayer: (username: string, password: string, email?: string) =>
    http<{ ok: boolean; id: number; uuid: string }>('POST', '/api/players', { username, password, email: email || null }),
  deletePlayer: (id: number) => http<{ ok: boolean }>('DELETE', `/api/players/${id}`),
  togglePlayerEnabled: (id: number, enabled: boolean) =>
    http<{ ok: boolean; enabled: boolean }>('POST', `/api/players/${id}/enabled`, { enabled }),
  //玩家扩展 修改 username/uuid 重置密码 皮肤管理
  updatePlayer: (id: number, data: { username?: string; uuid?: string; email?: string | null }) =>
    http<{ ok: boolean }>('PUT', `/api/players/${id}`, data),
  changePlayerPassword: (id: number, newPassword: string) =>
    http<{ ok: boolean }>('PUT', `/api/players/${id}/password`, { newPassword }),
  getPlayerSkinUrl: (id: number) => `/api/players/${id}/skin`,
  uploadPlayerSkin: (id: number, png: BufferSource, model: string) =>
    httpBinary('PUT', `/api/players/${id}/skin?model=${encodeURIComponent(model)}`, png, 'image/png'),
  changePlayerSkinModel: (id: number, model: string) =>
    http<{ ok: boolean; model: string }>('PATCH', `/api/players/${id}/skin-model`, { model }),
  deletePlayerSkin: (id: number) => http<{ ok: boolean }>('DELETE', `/api/players/${id}/skin`),

  //i18n 翻译 lang 缺失后端回退 config.Language
  getTranslations: (lang?: string) =>
    http<TranslationsData>('GET', '/i18n/translations' + (lang ? `?lang=${encodeURIComponent(lang)}` : '')),
  getLanguages: () => http<LanguagesData>('GET', '/i18n/languages'),
};
