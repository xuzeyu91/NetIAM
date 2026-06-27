import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_PORTAL_API_BASE ?? 'https://localhost:7003'
const TENANT_ID = import.meta.env.VITE_TENANT_ID ?? 'tenant-default'

type CallbackResult = {
  status: string
  userId?: string
  thirdPartyUserId?: string
  hint?: string
}

async function requestJson<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Id': TENANT_ID,
      ...(options?.headers ?? {}),
    },
  })

  const responseText = await response.text()
  if (!response.ok) {
    throw new Error(responseText || `Request failed (${response.status})`)
  }

  return responseText ? (JSON.parse(responseText) as T) : ({} as T)
}

function App() {
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

  async function startSso() {
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const response = await requestJson<{ authorizeUrl: string; state: string }>(
        `/authn/${provider}/${providerCode}?asJson=true`,
      )
      setState(response.state)
      setMessage('Authorize URL generated. Redirecting...')
      window.location.href = response.authorizeUrl
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to start SSO.')
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
        `/login/${provider}/${providerCode}?state=${encodeURIComponent(state)}&code=${encodeURIComponent(authorizationCode)}`,
      )
      setCallbackResult(response)
      setMessage(`Callback finished with status: ${response.status}`)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Callback failed.')
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
      await requestJson<{ status: string; userId: string }>('/login/bind', {
        method: 'POST',
        body: JSON.stringify({
          thirdPartyUserId: callbackResult.thirdPartyUserId,
          username: bindUsername,
          password: bindPassword,
        }),
      })
      setMessage('Account bound successfully.')
      setCallbackResult({ status: 'bound' })
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Bind failed.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="portal-layout">
      <section className="card">
        <h1>NetIAM Portal</h1>
        <p className="muted">Tenant: {TENANT_ID}</p>

        <label>Provider</label>
        <select value={provider} onChange={(event) => setProvider(event.target.value as typeof provider)}>
          <option value="dingtalk">DingTalk</option>
          <option value="feishu">Feishu</option>
          <option value="wecom">WeCom</option>
        </select>

        <label>Provider Code</label>
        <input value={providerCode} onChange={(event) => setProviderCode(event.target.value)} />

        <button disabled={loading} onClick={() => void startSso()}>
          Start SSO Authorization
        </button>
      </section>

      <section className="card">
        <h2>Callback Debugger</h2>
        <form onSubmit={submitCallback}>
          <label>State</label>
          <input value={state} onChange={(event) => setState(event.target.value)} required />

          <label>Authorization Code</label>
          <input value={authorizationCode} onChange={(event) => setAuthorizationCode(event.target.value)} required />

          <button type="submit" disabled={loading}>
            Submit Callback
          </button>
        </form>
      </section>

      {canBind && (
        <section className="card">
          <h2>Bind Existing Account</h2>
          <form onSubmit={bindAccount}>
            <label>Username</label>
            <input value={bindUsername} onChange={(event) => setBindUsername(event.target.value)} required />

            <label>Password</label>
            <input type="password" value={bindPassword} onChange={(event) => setBindPassword(event.target.value)} required />

            <button type="submit" disabled={loading}>
              Bind
            </button>
          </form>
        </section>
      )}

      {callbackResult && (
        <section className="card">
          <h2>Result</h2>
          <pre>{JSON.stringify(callbackResult, null, 2)}</pre>
        </section>
      )}

      {message && <div className="banner success">{message}</div>}
      {error && <div className="banner error">{error}</div>}
    </main>
  )
}

export default App
