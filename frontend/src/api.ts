import type { Session } from './types'

const KEY = 'bh_auth_v1'

let memory: Session | null = null

function read(): Session | null {
  if (memory) return memory
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    memory = JSON.parse(raw) as Session
    return memory
  } catch {
    return null
  }
}

export function getSession(): Session | null {
  return read()
}

export function setSession(s: Session): void {
  memory = s
  localStorage.setItem(KEY, JSON.stringify(s))
}

export function clearSession(): void {
  memory = null
  localStorage.removeItem(KEY)
}

function toQuery(q?: Record<string, unknown>): string {
  if (!q) return ''
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(q)) {
    if (v === undefined || v === null || v === '') continue
    params.set(k, String(v))
  }
  const s = params.toString()
  return s ? '?' + s : ''
}

async function parseError(res: Response): Promise<never> {
  let msg = 'حدث خطأ غير متوقع في الخادم.'
  try {
    const data = await res.json()
    if (typeof data === 'string' && data) msg = data
    else if (data?.message) msg = data.message
    else if (data?.detail) msg = data.detail
    else if (data?.title) msg = data.title
    else if (data?.errors) {
      const parts: string[] = []
      for (const v of Object.values(data.errors as object) as unknown[]) {
        if (Array.isArray(v)) parts.push(...(v as string[]))
        else if (v) parts.push(String(v))
      }
      if (parts.length) msg = parts.join(' / ')
    }
  } catch {
    try {
      msg = (await res.text()) || msg
    } catch {
      /* ignore */
    }
  }
  const err = new Error(msg) as Error & { status?: number }
  err.status = res.status
  throw err
}

let refreshing: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  const session = getSession()
  if (!session?.refreshToken) return false
  if (!refreshing) {
    refreshing = (async () => {
      try {
        const res = await fetch('/api/auth/refresh', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: session.refreshToken })
        })
        if (!res.ok) return false
        const data = await res.json()
        setSession(data as Session)
        return true
      } catch {
        return false
      } finally {
        refreshing = null
      }
    })()
  }
  return refreshing
}

function redirectLogin(): never {
  clearSession()
  if (!window.location.pathname.startsWith('/login')) {
    window.location.href = '/login'
  }
  throw new Error('انتهت الجلسة. يرجى تسجيل الدخول مرة أخرى.')
}

export async function request<T = unknown>(
  path: string,
  opts: {
    method?: string
    body?: unknown
    query?: Record<string, unknown>
    form?: FormData
    skipAuth?: boolean
  } = {}
): Promise<T> {
  const { method = 'GET', body, query, form, skipAuth } = opts
  const session = getSession()
  const headers: Record<string, string> = {}
  if (form) {
    // multipart: leave Content-Type unset
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (session && !skipAuth) headers['Authorization'] = 'Bearer ' + session.accessToken

  const init: RequestInit = { method, headers }
  if (form) init.body = form
  else if (body !== undefined) init.body = JSON.stringify(body)

  let res = await fetch(path + toQuery(query), init)

  if (res.status === 401 && session && !skipAuth) {
    const ok = await tryRefresh()
    if (ok) {
      const s2 = getSession()!
      const retry: RequestInit = {
        method,
        headers: { ...headers, Authorization: 'Bearer ' + s2.accessToken }
      }
      if (form) retry.body = form
      else if (body !== undefined) retry.body = JSON.stringify(body)
      res = await fetch(path + toQuery(query), retry)
    } else {
      redirectLogin()
    }
  }

  if (!res.ok) await parseError(res)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  if (!text) return undefined as T
  return JSON.parse(text) as T
}

export interface DownloadResult {
  blob: Blob
  fileName: string
}

export async function download(path: string, opts: { method?: string; body?: unknown } = {}): Promise<DownloadResult> {
  const session = getSession()
  const method = opts.method ?? 'GET'
  const headers: Record<string, string> = {}
  if (session) headers['Authorization'] = 'Bearer ' + session.accessToken
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json'
  const init: RequestInit = { method, headers }
  if (opts.body !== undefined) init.body = JSON.stringify(opts.body)

  let res = await fetch(path, init)
  if (res.status === 401 && session) {
    const ok = await tryRefresh()
    if (ok) {
      const s2 = getSession()!
      const retry: RequestInit = { method, headers: { ...headers, Authorization: 'Bearer ' + s2.accessToken } }
      if (opts.body !== undefined) retry.body = JSON.stringify(opts.body)
      res = await fetch(path, retry)
    } else {
      redirectLogin()
    }
  }
  if (!res.ok) await parseError(res)

  const disposition = res.headers.get('Content-Disposition') ?? ''
  const m = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition)
  let fileName = 'download'
  if (m) fileName = decodeURIComponent(m[1].replace(/"/g, ''))
  return { blob: await res.blob(), fileName }
}

export function triggerDownload(result: DownloadResult): void {
  const url = URL.createObjectURL(result.blob)
  const a = document.createElement('a')
  a.href = url
  a.download = result.fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 4000)
}

export const api = {
  get: <T = unknown>(path: string, query?: Record<string, unknown>) => request<T>(path, { query }),
  post: <T = unknown>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),
  put: <T = unknown>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  del: <T = unknown>(path: string) => request<T>(path, { method: 'DELETE' })
}
