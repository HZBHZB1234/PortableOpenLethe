import { pushLog } from './log'

/**
 * Every /admin/* endpoint except /admin/login authenticates with the token in
 * the JSON BODY (see Handlers/AdminEndpoints.cs `Admin(...)` wrapper) — a Bearer
 * header alone is not enough. We send both: the header is harmless and the body
 * token is what the server actually verifies.
 */
export const TOKEN_KEY = 'pl_admin_token'

export interface ApiResult {
  ok: boolean
  status: number
  data: any
  text: string
}

export function getToken(): string {
  return sessionStorage.getItem(TOKEN_KEY) ?? ''
}
export function setToken(token: string) {
  sessionStorage.setItem(TOKEN_KEY, token)
}
export function clearToken() {
  sessionStorage.removeItem(TOKEN_KEY)
}

export async function loginAdmin(username: string, password: string): Promise<string> {
  const res = await fetch('/admin/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    // The server reads `password` only; `username` is included for the shared
    // login form and is ignored by the endpoint.
    body: JSON.stringify({ username, password }),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok || !data?.token) throw new Error('登录失败，请检查管理密码')
  return data.token as string
}

export async function call(path: string, body: Record<string, unknown> = {}): Promise<ApiResult> {
  const token = getToken()
  const started = performance.now()
  const res = await fetch(path, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ ...body, token }),
  })
  const text = await res.text()
  let data: any = null
  try { data = JSON.parse(text) } catch { /* plain-text endpoints such as /config/schema */ }

  pushLog({ path, status: res.status, ms: Math.round(performance.now() - started), preview: text.slice(0, 500) })

  if (res.status === 401) {
    clearToken()
    window.dispatchEvent(new CustomEvent('pl-admin-unauthorized'))
  }
  return { ok: res.ok, status: res.status, data, text }
}

/** Exact whitelist from AdminEndpoints.DocumentFields — unknown fields are 400. */
export const DOCUMENT_FIELDS = [
  'Formations', 'Egos', 'Personalities', 'Announcers', 'UserInfo', 'CustomTheme',
  'CustomIdentities', 'CustomEgos', 'CustomEgoGifts', 'CustomEgoGiftDropPools',
  'CustomMdBattles', 'CustomMdCommonData', 'MdSaveInfo', 'StorySaveInfo',
  'StoryMdSaveInfo', 'RailwaySaveInfo', 'ChapterState', 'Chapter10RpgSaveInfo',
  'BossRaidSaveInfo',
] as const
