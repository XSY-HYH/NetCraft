//Player API 客户端 主端口玩家自服务 API
//cookie session credentials include 后端 HttpOnly cookie 前端不持 token
//响应 { ok, ... } 非 ok 或非 2xx 抛 PlayerError 含 status 与 error 字段
//401 未登录 由调用方处理跳转 /login

export class PlayerError extends Error {
  constructor (message: string, public status: number, public error?: string) {
    super(message);
    this.name = 'PlayerError';
  }
}

//http 通用请求封装 返回解析后 JSON 非 ok 抛 PlayerError
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
  if (text) { try { data = JSON.parse(text); } catch { data = { message: text }; } }
  if (!res.ok || data.ok === false) {
    throw new PlayerError(data.message || data.error || `HTTP ${res.status}`, res.status, data.error);
  }
  return data as T;
}

export interface PlayerSessionInfo {
  ok: boolean;
  username?: string;
  uuid?: string;
  email?: string | null;
  emailVerified?: boolean;
  //个性化偏好 NULL 未设置 前端按浏览器/系统回退
  language?: string | null;
  theme?: string | null;
}

export interface PlayerProfile {
  ok: boolean;
  id?: number;
  uuid?: string;
  username?: string;
  email?: string | null;
  emailVerified?: boolean;
  //OAuth 绑定信息 null 密码账户 非空 OAuth 账户
  oauthProvider?: string | null;
  //注册时间 Home 账户安全卡展示
  createdAt?: string;
  //皮肤元数据 3D 预览用
  skinHash?: string | null;
  skinModel?: string | null;
  capeHash?: string | null;
}

//OAuth 提供商 后端配置驱动 前端按列表渲染登录按钮
export interface OAuthProvider {
  key: string;
  name: string;
}

//补全注册预填 OAuth 暂存身份 username/email 可改
export interface PendingOAuthInfo {
  ok: boolean;
  provider?: string;
  username?: string;
  email?: string | null;
}

export const playerApi = {
  //session 检查与登录登出
  getSession: () => http<PlayerSessionInfo>('GET', '/api/player/session'),
  login: (username: string, password: string) =>
    http<{ ok: boolean; username: string; uuid: string }>('POST', '/api/player/login', { username, password }),
  logout: () => http<{ ok: boolean }>('POST', '/api/player/logout'),
  //档案管理 GET 取概览 PUT 改 username/email(email null 不变 空串清空)
  getProfile: () => http<PlayerProfile>('GET', '/api/player/profile'),
  updateProfile: (username: string, email: string | null) =>
    http<{ ok: boolean }>('PUT', '/api/player/profile', { username, email }),
  //改密 旧密码校验 新密码至少 4 位
  changePassword: (oldPassword: string, newPassword: string) =>
    http<{ ok: boolean }>('PUT', '/api/player/password', { oldPassword, newPassword }),
  //OAuth 提供商列表 前端按此渲染登录按钮 配置驱动 留空则无按钮
  getOAuthProviders: () => http<{ ok: boolean; providers: OAuthProvider[] }>('GET', '/api/player/oauth/providers'),
  //OAuth 发起登录 浏览器跳转后端 Challenge 重定向到 provider authorize
  oauthLogin: (key: string) => { window.location.href = `/api/player/oauth/${key}/login`; },
  //补全注册 取暂存身份预填 token 来自 OAuth 回调重定向 query
  getPendingOAuth: (token: string) =>
    http<PendingOAuthInfo>('GET', `/api/player/register?token=${encodeURIComponent(token)}`),
  //补全注册 建 OAuth 账户绑定 provider+subject 签 session
  register: (token: string, username: string, email: string | null) =>
    http<{ ok: boolean }>('POST', '/api/player/register', { token, username, email }),
  //个性化偏好 language/theme null 不改 空串清空
  updatePreferences: (language?: string | null, theme?: string | null) =>
    http<{ ok: boolean }>('PUT', '/api/player/preferences', { language, theme }),
};
