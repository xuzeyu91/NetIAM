export type RequestContext = {
  apiBase: string
  portalBase: string
  tenantId: string
  actingUserId: string
  bearerToken: string
}

const REQUEST_CONTEXT_STORAGE_KEY = 'netiam-admin-request-context'
const DEFAULT_API_BASE = import.meta.env.VITE_ADMIN_API_BASE ?? 'https://localhost:7002'
const DEFAULT_PORTAL_BASE = import.meta.env.VITE_PORTAL_API_BASE ?? 'https://localhost:7003'
const DEFAULT_TENANT_ID = import.meta.env.VITE_TENANT_ID ?? 'tenant-default'
const DEFAULT_ACTING_USER_ID = import.meta.env.VITE_ACTING_USER_ID ?? 'user-admin-default'

function normalizeBaseUrl(rawBase: string, fallback: string): string {
  const candidate = rawBase.trim() || fallback
  return candidate.endsWith('/') ? candidate.slice(0, -1) : candidate
}

function normalizeContext(value: Partial<RequestContext>, fallback: RequestContext): RequestContext {
  return {
    apiBase: normalizeBaseUrl(value.apiBase ?? fallback.apiBase, fallback.apiBase),
    portalBase: normalizeBaseUrl(value.portalBase ?? fallback.portalBase, fallback.portalBase),
    tenantId: value.tenantId?.trim() || fallback.tenantId,
    actingUserId: value.actingUserId?.trim() || fallback.actingUserId,
    bearerToken: value.bearerToken?.trim() ?? fallback.bearerToken,
  }
}

export function createDefaultRequestContext(): RequestContext {
  return {
    apiBase: normalizeBaseUrl(DEFAULT_API_BASE, 'https://localhost:7002'),
    portalBase: normalizeBaseUrl(DEFAULT_PORTAL_BASE, 'https://localhost:7003'),
    tenantId: DEFAULT_TENANT_ID,
    actingUserId: DEFAULT_ACTING_USER_ID,
    bearerToken: '',
  }
}

export function createInitialRequestContext(): RequestContext {
  const fallback = createDefaultRequestContext()
  if (typeof window === 'undefined') {
    return fallback
  }

  try {
    const raw = window.localStorage.getItem(REQUEST_CONTEXT_STORAGE_KEY)
    if (!raw) {
      return fallback
    }

    const parsed = JSON.parse(raw) as Partial<RequestContext>
    return normalizeContext(parsed, fallback)
  } catch {
    return fallback
  }
}

export function persistRequestContext(context: RequestContext): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(REQUEST_CONTEXT_STORAGE_KEY, JSON.stringify(context))
  } catch {
    // Ignore persistence failures (private mode or quota issues).
  }
}

export async function requestJson<T>(context: RequestContext, path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers ?? {})
  const hasBody = options?.body !== undefined && options?.body !== null
  if (hasBody && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const tenantId = context.tenantId.trim()
  if (tenantId) {
    headers.set('X-Tenant-Id', tenantId)
  }

  const actingUserId = context.actingUserId.trim()
  if (actingUserId) {
    headers.set('X-Acting-User-Id', actingUserId)
  }

  const token = context.bearerToken.trim()
  if (token) {
    const normalizedToken = token.toLowerCase().startsWith('bearer ') ? token : `Bearer ${token}`
    headers.set('Authorization', normalizedToken)
  }

  const apiBase = normalizeBaseUrl(context.apiBase, DEFAULT_API_BASE)
  const response = await fetch(`${apiBase}${path}`, {
    ...options,
    headers,
  })

  const responseText = await response.text()
  if (!response.ok) {
    throw new Error(responseText || `Request failed (${response.status})`)
  }

  if (!responseText.trim()) {
    return {} as T
  }

  try {
    return JSON.parse(responseText) as T
  } catch {
    return responseText as T
  }
}
