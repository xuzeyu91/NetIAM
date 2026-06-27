import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

const DEFAULT_PORTAL_BASE = import.meta.env.VITE_PORTAL_API_BASE ?? 'https://localhost:7003'
const DEFAULT_TENANT_ID = import.meta.env.VITE_TENANT_ID ?? 'tenant-default'
const DEFAULT_ACTING_USER_ID = import.meta.env.VITE_ACTING_USER_ID ?? ''
const CONTEXT_STORAGE_KEY = 'netiam-portal-request-context'

type PortalView = 'applications' | 'account' | 'bindings' | 'audit' | 'sessions' | 'sso'

type RequestContext = {
  portalBase: string
  tenantId: string
  actingUserId: string
  sessionId: string
  bearerToken: string
}

type UserProfile = {
  id: string
  userName?: string
  displayName: string
  email?: string
  phoneNumber?: string
  externalId?: string
  emailConfirmed: boolean
  phoneNumberConfirmed: boolean
  twoFactorEnabled: boolean
  lockoutEnd?: string
  accessFailedCount: number
  createTime?: string
  updateTime?: string
}

type PortalApplication = {
  id: string
  code: string
  name: string
  protocol: string
  enabled: boolean
}

type BindingItem = {
  id: string
  boundTime: string
  providerId: string
  providerCode?: string
  providerName?: string
  providerType?: number
  openId: string
  unionId?: string
  name?: string
  email?: string
  mobile?: string
  avatarUrl?: string
  lastLoginTime: string
}

type AuditEntry = {
  id: string
  eventType: string
  content: string
  resultStatus: string
  requestId?: string
  sessionId?: string
  userAgent?: string
  ipAddress?: string
  geoLocation?: string
  occurredTime: string
}

type SessionItem = {
  sessionId: string
  eventType: string
  resultStatus: string
  ipAddress?: string
  userAgent?: string
  occurredTime: string
  revoked: boolean
}

type CallbackResult = {
  status: string
  userId?: string
  thirdPartyUserId?: string
  sessionId?: string
  hint?: string
}

function normalizeBaseUrl(value: string): string {
  const candidate = value.trim() || DEFAULT_PORTAL_BASE
  return candidate.endsWith('/') ? candidate.slice(0, -1) : candidate
}

function createDefaultContext(): RequestContext {
  return {
    portalBase: normalizeBaseUrl(DEFAULT_PORTAL_BASE),
    tenantId: DEFAULT_TENANT_ID,
    actingUserId: DEFAULT_ACTING_USER_ID,
    sessionId: '',
    bearerToken: '',
  }
}

function normalizeContext(value: Partial<RequestContext>, fallback: RequestContext): RequestContext {
  return {
    portalBase: normalizeBaseUrl(value.portalBase ?? fallback.portalBase),
    tenantId: value.tenantId?.trim() || fallback.tenantId,
    actingUserId: value.actingUserId?.trim() ?? fallback.actingUserId,
    sessionId: value.sessionId?.trim() ?? fallback.sessionId,
    bearerToken: value.bearerToken?.trim() ?? fallback.bearerToken,
  }
}

function createInitialContext(): RequestContext {
  const fallback = createDefaultContext()
  if (typeof window === 'undefined') {
    return fallback
  }

  try {
    const raw = window.localStorage.getItem(CONTEXT_STORAGE_KEY)
    if (!raw) {
      return fallback
    }

    return normalizeContext(JSON.parse(raw) as Partial<RequestContext>, fallback)
  } catch {
    return fallback
  }
}

function persistContext(context: RequestContext): void {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(CONTEXT_STORAGE_KEY, JSON.stringify(context))
}

async function requestJson<T>(context: RequestContext, path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers ?? {})
  if (options?.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  if (context.tenantId.trim()) {
    headers.set('X-Tenant-Id', context.tenantId.trim())
  }

  if (context.actingUserId.trim()) {
    headers.set('X-Acting-User-Id', context.actingUserId.trim())
  }

  if (context.sessionId.trim()) {
    headers.set('X-Session-Id', context.sessionId.trim())
  }

  if (context.bearerToken.trim()) {
    const token = context.bearerToken.trim()
    headers.set('Authorization', token.toLowerCase().startsWith('bearer ') ? token : `Bearer ${token}`)
  }

  const response = await fetch(`${normalizeBaseUrl(context.portalBase)}${path}`, {
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

function formatDate(value?: string): string {
  if (!value) {
    return '-'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function resolveError(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback
}

function App() {
  const [context, setContext] = useState<RequestContext>(() => createInitialContext())
  const [draftContext, setDraftContext] = useState<RequestContext>(context)
  const [activeView, setActiveView] = useState<PortalView>('applications')
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [applications, setApplications] = useState<PortalApplication[]>([])
  const [bindings, setBindings] = useState<BindingItem[]>([])
  const [auditEntries, setAuditEntries] = useState<AuditEntry[]>([])
  const [sessions, setSessions] = useState<SessionItem[]>([])
  const [profileForm, setProfileForm] = useState({ displayName: '', email: '', phoneNumber: '' })
  const [passwordForm, setPasswordForm] = useState({ currentPassword: '', newPassword: '' })
  const [provider, setProvider] = useState<'dingtalk' | 'feishu' | 'wecom'>('dingtalk')
  const [providerCode, setProviderCode] = useState('default')
  const [state, setState] = useState('')
  const [authorizationCode, setAuthorizationCode] = useState('')
  const [callbackResult, setCallbackResult] = useState<CallbackResult | null>(null)
  const [bindUsername, setBindUsername] = useState('')
  const [bindPassword, setBindPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const canBind = useMemo(() => {
    return callbackResult?.status === 'pending_binding' && !!callbackResult.thirdPartyUserId
  }, [callbackResult])

  const currentSessionId = context.sessionId.trim()

  async function loadPortalData(currentContext = context): Promise<void> {
    setLoading(true)
    setError('')
    try {
      const [nextProfile, nextApplications, nextBindings, nextAuditEntries, nextSessions] = await Promise.all([
        requestJson<UserProfile>(currentContext, '/api/portal/me'),
        requestJson<PortalApplication[]>(currentContext, '/api/portal/apps'),
        requestJson<BindingItem[]>(currentContext, '/api/portal/bindings'),
        requestJson<AuditEntry[]>(currentContext, '/api/portal/audit?take=50'),
        requestJson<SessionItem[]>(currentContext, '/api/portal/sessions?take=50'),
      ])

      setProfile(nextProfile)
      setProfileForm({
        displayName: nextProfile.displayName ?? '',
        email: nextProfile.email ?? '',
        phoneNumber: nextProfile.phoneNumber ?? '',
      })
      setApplications(nextApplications)
      setBindings(nextBindings)
      setAuditEntries(nextAuditEntries)
      setSessions(nextSessions)
    } catch (requestError) {
      setError(resolveError(requestError, 'Portal data failed to load.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadPortalData()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function saveContext(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextContext = normalizeContext(draftContext, createDefaultContext())
    setContext(nextContext)
    persistContext(nextContext)
    setMessage('Context saved.')
    void loadPortalData(nextContext)
  }

  async function updateProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const nextProfile = await requestJson<UserProfile>(context, '/api/portal/me', {
        method: 'PUT',
        body: JSON.stringify(profileForm),
      })
      setProfile(nextProfile)
      setProfileForm({
        displayName: nextProfile.displayName ?? '',
        email: nextProfile.email ?? '',
        phoneNumber: nextProfile.phoneNumber ?? '',
      })
      setMessage('Profile updated.')
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Profile update failed.'))
    } finally {
      setLoading(false)
    }
  }

  async function changePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await requestJson<{ changed: boolean }>(context, '/api/portal/me/change-password', {
        method: 'POST',
        body: JSON.stringify(passwordForm),
      })
      setPasswordForm({ currentPassword: '', newPassword: '' })
      setMessage('Password changed.')
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Password change failed.'))
    } finally {
      setLoading(false)
    }
  }

  async function unbind(bindingId: string) {
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await requestJson<Record<string, never>>(context, `/api/portal/bindings/${encodeURIComponent(bindingId)}`, {
        method: 'DELETE',
      })
      setMessage('Binding removed.')
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Unbind failed.'))
    } finally {
      setLoading(false)
    }
  }

  async function revokeSession(sessionId: string) {
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await requestJson<{ revoked: boolean }>(context, `/api/portal/sessions/${encodeURIComponent(sessionId)}/revoke`, {
        method: 'POST',
      })
      setMessage('Session revoked.')
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Session revoke failed.'))
    } finally {
      setLoading(false)
    }
  }

  async function startSso() {
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const response = await requestJson<{ authorizeUrl: string; state: string }>(
        context,
        `/authn/${provider}/${providerCode}?asJson=true`,
      )
      setState(response.state)
      setMessage('Authorization started.')
      window.location.href = response.authorizeUrl
    } catch (requestError) {
      setError(resolveError(requestError, 'Failed to start SSO.'))
    } finally {
      setLoading(false)
    }
  }

  async function submitCallback(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const response = await requestJson<CallbackResult>(
        context,
        `/login/${provider}/${providerCode}?state=${encodeURIComponent(state)}&code=${encodeURIComponent(authorizationCode)}`,
      )
      setCallbackResult(response)
      setMessage(`Callback finished: ${response.status}`)
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Callback failed.'))
    } finally {
      setLoading(false)
    }
  }

  async function bindAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!callbackResult?.thirdPartyUserId) {
      return
    }

    setLoading(true)
    setError('')
    setMessage('')
    try {
      await requestJson<{ status: string; userId: string }>(context, '/login/bind', {
        method: 'POST',
        body: JSON.stringify({
          thirdPartyUserId: callbackResult.thirdPartyUserId,
          username: bindUsername,
          password: bindPassword,
        }),
      })
      setMessage('Account bound.')
      setCallbackResult({ status: 'bound' })
      setBindPassword('')
      await loadPortalData(context)
    } catch (requestError) {
      setError(resolveError(requestError, 'Bind failed.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="portal-shell">
      <header className="topbar">
        <div>
          <h1>NetIAM Portal</h1>
          <p>{profile ? `${profile.displayName} / ${profile.userName ?? profile.id}` : 'Self-service console'}</p>
        </div>
        <button type="button" className="secondary" disabled={loading} onClick={() => void loadPortalData()}>
          Refresh
        </button>
      </header>

      <form className="context-strip" onSubmit={saveContext}>
        <label>
          API
          <input
            value={draftContext.portalBase}
            onChange={(event) => setDraftContext({ ...draftContext, portalBase: event.target.value })}
          />
        </label>
        <label>
          Tenant
          <input
            value={draftContext.tenantId}
            onChange={(event) => setDraftContext({ ...draftContext, tenantId: event.target.value })}
          />
        </label>
        <label>
          User ID
          <input
            value={draftContext.actingUserId}
            onChange={(event) => setDraftContext({ ...draftContext, actingUserId: event.target.value })}
          />
        </label>
        <label>
          Session ID
          <input
            value={draftContext.sessionId}
            onChange={(event) => setDraftContext({ ...draftContext, sessionId: event.target.value })}
          />
        </label>
        <label>
          Token
          <input
            type="password"
            value={draftContext.bearerToken}
            onChange={(event) => setDraftContext({ ...draftContext, bearerToken: event.target.value })}
          />
        </label>
        <button type="submit" disabled={loading}>
          Save
        </button>
      </form>

      <nav className="tabs" aria-label="Portal navigation">
        {[
          ['applications', 'Applications'],
          ['account', 'Account'],
          ['bindings', 'Bindings'],
          ['audit', 'Audit'],
          ['sessions', 'Sessions'],
          ['sso', 'SSO'],
        ].map(([view, label]) => (
          <button
            key={view}
            type="button"
            className={activeView === view ? 'active' : ''}
            onClick={() => setActiveView(view as PortalView)}
          >
            {label}
          </button>
        ))}
      </nav>

      {message && <div className="banner success">{message}</div>}
      {error && <div className="banner error">{error}</div>}

      {activeView === 'applications' && (
        <section className="panel">
          <div className="panel-title">
            <h2>Applications</h2>
            <span>{applications.length}</span>
          </div>
          <div className="app-grid">
            {applications.map((app) => (
              <article className="app-tile" key={app.id}>
                <strong>{app.name}</strong>
                <span>{app.code}</span>
                <div>
                  <mark>{app.protocol}</mark>
                </div>
              </article>
            ))}
            {applications.length === 0 && <p className="empty">No assigned applications.</p>}
          </div>
        </section>
      )}

      {activeView === 'account' && (
        <section className="account-grid">
          <form className="panel" onSubmit={updateProfile}>
            <div className="panel-title">
              <h2>Profile</h2>
              <span>{profile?.id ?? '-'}</span>
            </div>
            <label>
              Display name
              <input
                value={profileForm.displayName}
                onChange={(event) => setProfileForm({ ...profileForm, displayName: event.target.value })}
                required
              />
            </label>
            <label>
              Email
              <input
                type="email"
                value={profileForm.email}
                onChange={(event) => setProfileForm({ ...profileForm, email: event.target.value })}
              />
            </label>
            <label>
              Phone
              <input
                value={profileForm.phoneNumber}
                onChange={(event) => setProfileForm({ ...profileForm, phoneNumber: event.target.value })}
              />
            </label>
            <button type="submit" disabled={loading}>
              Update Profile
            </button>
          </form>

          <form className="panel" onSubmit={changePassword}>
            <div className="panel-title">
              <h2>Password</h2>
              <span>{profile?.accessFailedCount ?? 0} failed</span>
            </div>
            <label>
              Current password
              <input
                type="password"
                value={passwordForm.currentPassword}
                onChange={(event) => setPasswordForm({ ...passwordForm, currentPassword: event.target.value })}
                required
              />
            </label>
            <label>
              New password
              <input
                type="password"
                value={passwordForm.newPassword}
                onChange={(event) => setPasswordForm({ ...passwordForm, newPassword: event.target.value })}
                required
              />
            </label>
            <button type="submit" disabled={loading}>
              Change Password
            </button>
          </form>
        </section>
      )}

      {activeView === 'bindings' && (
        <section className="panel">
          <div className="panel-title">
            <h2>Bindings</h2>
            <span>{bindings.length}</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Provider</th>
                  <th>Open ID</th>
                  <th>Name</th>
                  <th>Bound</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {bindings.map((binding) => (
                  <tr key={binding.id}>
                    <td>{binding.providerName ?? binding.providerCode ?? binding.providerId}</td>
                    <td>{binding.openId}</td>
                    <td>{binding.name ?? '-'}</td>
                    <td>{formatDate(binding.boundTime)}</td>
                    <td>
                      <button type="button" className="danger" disabled={loading} onClick={() => void unbind(binding.id)}>
                        Unbind
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {bindings.length === 0 && <p className="empty">No identity bindings.</p>}
        </section>
      )}

      {activeView === 'audit' && (
        <section className="panel">
          <div className="panel-title">
            <h2>Audit</h2>
            <span>{auditEntries.length}</span>
          </div>
          <div className="timeline">
            {auditEntries.map((entry) => (
              <article key={entry.id}>
                <time>{formatDate(entry.occurredTime)}</time>
                <strong>{entry.eventType}</strong>
                <p>{entry.content}</p>
                <span>{entry.resultStatus}</span>
              </article>
            ))}
            {auditEntries.length === 0 && <p className="empty">No audit entries.</p>}
          </div>
        </section>
      )}

      {activeView === 'sessions' && (
        <section className="panel">
          <div className="panel-title">
            <h2>Sessions</h2>
            <span>{sessions.length}</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Session</th>
                  <th>IP</th>
                  <th>Time</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((session) => (
                  <tr key={session.sessionId}>
                    <td>{session.sessionId}</td>
                    <td>{session.ipAddress ?? '-'}</td>
                    <td>{formatDate(session.occurredTime)}</td>
                    <td>{session.revoked ? 'Revoked' : 'Active'}</td>
                    <td>
                      <button
                        type="button"
                        className="danger"
                        disabled={loading || session.revoked || session.sessionId === currentSessionId}
                        onClick={() => void revokeSession(session.sessionId)}
                      >
                        Revoke
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {sessions.length === 0 && <p className="empty">No sessions.</p>}
        </section>
      )}

      {activeView === 'sso' && (
        <section className="sso-grid">
          <div className="panel">
            <div className="panel-title">
              <h2>Authorization</h2>
              <span>{provider}</span>
            </div>
            <label>
              Provider
              <select value={provider} onChange={(event) => setProvider(event.target.value as typeof provider)}>
                <option value="dingtalk">DingTalk</option>
                <option value="feishu">Feishu</option>
                <option value="wecom">WeCom</option>
              </select>
            </label>
            <label>
              Provider code
              <input value={providerCode} onChange={(event) => setProviderCode(event.target.value)} />
            </label>
            <button type="button" disabled={loading} onClick={() => void startSso()}>
              Start SSO
            </button>
          </div>

          <form className="panel" onSubmit={submitCallback}>
            <div className="panel-title">
              <h2>Callback</h2>
              <span>{callbackResult?.status ?? '-'}</span>
            </div>
            <label>
              State
              <input value={state} onChange={(event) => setState(event.target.value)} required />
            </label>
            <label>
              Code
              <input value={authorizationCode} onChange={(event) => setAuthorizationCode(event.target.value)} required />
            </label>
            <button type="submit" disabled={loading}>
              Submit Callback
            </button>
          </form>

          {canBind && (
            <form className="panel" onSubmit={bindAccount}>
              <div className="panel-title">
                <h2>Bind Account</h2>
                <span>{callbackResult?.thirdPartyUserId}</span>
              </div>
              <label>
                Username
                <input value={bindUsername} onChange={(event) => setBindUsername(event.target.value)} required />
              </label>
              <label>
                Password
                <input
                  type="password"
                  value={bindPassword}
                  onChange={(event) => setBindPassword(event.target.value)}
                  required
                />
              </label>
              <button type="submit" disabled={loading}>
                Bind
              </button>
            </form>
          )}

          {callbackResult && (
            <div className="panel result-panel">
              <div className="panel-title">
                <h2>Result</h2>
                <span>{callbackResult.status}</span>
              </div>
              <pre>{JSON.stringify(callbackResult, null, 2)}</pre>
            </div>
          )}
        </section>
      )}
    </main>
  )
}

export default App
