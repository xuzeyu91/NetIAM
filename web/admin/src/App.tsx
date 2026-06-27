import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type UserItem = {
  id: string
  userName: string
  displayName: string
  email?: string
  phoneNumber?: string
  externalId?: string
}

type IdentityProviderItem = {
  id: string
  code: string
  name: string
  providerType: number
  enabled: boolean
}

type IdentitySourceItem = {
  id: string
  code: string
  name: string
  providerType: number
  enabled: boolean
}

type AuditItem = {
  id: string
  eventType: string
  content: string
  resultStatus: string
  occurredTime: string
}

const API_BASE = import.meta.env.VITE_ADMIN_API_BASE ?? 'https://localhost:7002'
const TENANT_ID = import.meta.env.VITE_TENANT_ID ?? 'tenant-default'

const providerMap: Record<number, string> = {
  1: 'DingTalk',
  2: 'Feishu',
  3: 'WeCom',
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

  if (!response.ok) {
    const message = await response.text()
    throw new Error(message || `Request failed (${response.status})`)
  }

  return (await response.json()) as T
}

function App() {
  const [loading, setLoading] = useState(false)
  const [activeTab, setActiveTab] = useState<'users' | 'providers' | 'sources' | 'audit'>('users')
  const [users, setUsers] = useState<UserItem[]>([])
  const [providers, setProviders] = useState<IdentityProviderItem[]>([])
  const [sources, setSources] = useState<IdentitySourceItem[]>([])
  const [audits, setAudits] = useState<AuditItem[]>([])
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [providerCode, setProviderCode] = useState('')
  const [providerName, setProviderName] = useState('')
  const [providerType, setProviderType] = useState('1')
  const [sourceCode, setSourceCode] = useState('')
  const [sourceName, setSourceName] = useState('')
  const [sourceType, setSourceType] = useState('1')

  const dashboardMetrics = useMemo(() => {
    return [
      { label: 'Users', value: users.length },
      { label: 'Identity Providers', value: providers.length },
      { label: 'Identity Sources', value: sources.length },
      { label: 'Recent Audits', value: audits.length },
    ]
  }, [audits.length, providers.length, sources.length, users.length])

  async function refreshAll() {
    setLoading(true)
    setError('')
    try {
      const [userData, providerData, sourceData, auditData] = await Promise.all([
        requestJson<UserItem[]>('/api/admin/users'),
        requestJson<IdentityProviderItem[]>('/api/admin/identity-providers'),
        requestJson<IdentitySourceItem[]>('/api/admin/identity-sources'),
        requestJson<AuditItem[]>('/api/admin/audit?take=50'),
      ])
      setUsers(userData)
      setProviders(providerData)
      setSources(sourceData)
      setAudits(auditData)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to load admin data.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refreshAll()
  }, [])

  async function handleCreateProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson('/api/admin/identity-providers', {
        method: 'POST',
        body: JSON.stringify({
          code: providerCode,
          name: providerName,
          providerType: Number(providerType),
          configJson: '{}',
        }),
      })
      setProviderCode('')
      setProviderName('')
      setSuccess('Identity Provider created.')
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to create provider.')
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson('/api/admin/identity-sources', {
        method: 'POST',
        body: JSON.stringify({
          code: sourceCode,
          name: sourceName,
          providerType: Number(sourceType),
          basicConfigJson: '{}',
        }),
      })
      setSourceCode('')
      setSourceName('')
      setSuccess('Identity Source created.')
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to create source.')
    } finally {
      setLoading(false)
    }
  }

  async function handleTriggerSync(code: string) {
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-sources/${code}/sync`, { method: 'POST' })
      setSuccess(`Sync triggered for ${code}.`)
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to trigger sync.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="layout">
      <aside className="sidebar">
        <h1>NetIAM Admin</h1>
        <p className="muted">Tenant: {TENANT_ID}</p>
        <button onClick={() => setActiveTab('users')} className={activeTab === 'users' ? 'active' : ''}>
          Users
        </button>
        <button onClick={() => setActiveTab('providers')} className={activeTab === 'providers' ? 'active' : ''}>
          Identity Providers
        </button>
        <button onClick={() => setActiveTab('sources')} className={activeTab === 'sources' ? 'active' : ''}>
          Identity Sources
        </button>
        <button onClick={() => setActiveTab('audit')} className={activeTab === 'audit' ? 'active' : ''}>
          Audit
        </button>
        <button onClick={() => void refreshAll()}>Refresh</button>
      </aside>

      <section className="content">
        <header className="metrics">
          {dashboardMetrics.map((metric) => (
            <div key={metric.label} className="card">
              <div className="metric-value">{metric.value}</div>
              <div className="metric-label">{metric.label}</div>
            </div>
          ))}
        </header>

        {loading && <div className="banner info">Loading...</div>}
        {error && <div className="banner error">{error}</div>}
        {success && <div className="banner success">{success}</div>}

        {activeTab === 'users' && (
          <div className="panel">
            <h2>Users</h2>
            <table>
              <thead>
                <tr>
                  <th>Username</th>
                  <th>Display Name</th>
                  <th>Email</th>
                  <th>External Id</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>{user.userName}</td>
                    <td>{user.displayName}</td>
                    <td>{user.email ?? '-'}</td>
                    <td>{user.externalId ?? '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'providers' && (
          <div className="panel">
            <h2>Identity Providers</h2>
            <form onSubmit={handleCreateProvider} className="inline-form">
              <input placeholder="Code" value={providerCode} onChange={(event) => setProviderCode(event.target.value)} required />
              <input placeholder="Name" value={providerName} onChange={(event) => setProviderName(event.target.value)} required />
              <select value={providerType} onChange={(event) => setProviderType(event.target.value)}>
                <option value="1">DingTalk</option>
                <option value="2">Feishu</option>
                <option value="3">WeCom</option>
              </select>
              <button type="submit">Create</button>
            </form>
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Type</th>
                  <th>Enabled</th>
                </tr>
              </thead>
              <tbody>
                {providers.map((provider) => (
                  <tr key={provider.id}>
                    <td>{provider.code}</td>
                    <td>{provider.name}</td>
                    <td>{providerMap[provider.providerType] ?? provider.providerType}</td>
                    <td>{provider.enabled ? 'Yes' : 'No'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'sources' && (
          <div className="panel">
            <h2>Identity Sources</h2>
            <form onSubmit={handleCreateSource} className="inline-form">
              <input placeholder="Code" value={sourceCode} onChange={(event) => setSourceCode(event.target.value)} required />
              <input placeholder="Name" value={sourceName} onChange={(event) => setSourceName(event.target.value)} required />
              <select value={sourceType} onChange={(event) => setSourceType(event.target.value)}>
                <option value="1">DingTalk</option>
                <option value="2">Feishu</option>
                <option value="3">WeCom</option>
              </select>
              <button type="submit">Create</button>
            </form>
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Type</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {sources.map((source) => (
                  <tr key={source.id}>
                    <td>{source.code}</td>
                    <td>{source.name}</td>
                    <td>{providerMap[source.providerType] ?? source.providerType}</td>
                    <td>
                      <button onClick={() => void handleTriggerSync(source.code)}>Sync now</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'audit' && (
          <div className="panel">
            <h2>Audit</h2>
            <table>
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Event Type</th>
                  <th>Status</th>
                  <th>Content</th>
                </tr>
              </thead>
              <tbody>
                {audits.map((audit) => (
                  <tr key={audit.id}>
                    <td>{new Date(audit.occurredTime).toLocaleString()}</td>
                    <td>{audit.eventType}</td>
                    <td>{audit.resultStatus}</td>
                    <td>{audit.content}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  )
}

export default App
