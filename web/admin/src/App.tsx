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
  configJson: string
}

type IdentitySourceItem = {
  id: string
  code: string
  name: string
  providerType: number
  enabled: boolean
  basicConfigJson: string
  strategyConfigJson?: string
  jobConfigJson?: string
}

type AuditItem = {
  id: string
  eventType: string
  content: string
  resultStatus: string
  occurredTime: string
}

type ProviderTypeValue = '1' | '2' | '3'

type ProviderDraft = {
  code: string
  name: string
  providerType: ProviderTypeValue
  enabled: boolean
  appKey: string
  appId: string
  corpId: string
  agentId: string
  appSecret: string
}

type SourceDraft = {
  code: string
  name: string
  providerType: ProviderTypeValue
  enabled: boolean
  useMock: boolean
  appKey: string
  appId: string
  corpId: string
  appSecret: string
  rootDeptId: string
  rootDeptName: string
  strategyConfigJson: string
  jobConfigJson: string
}

const API_BASE = import.meta.env.VITE_ADMIN_API_BASE ?? 'https://localhost:7002'
const TENANT_ID = import.meta.env.VITE_TENANT_ID ?? 'tenant-default'
const PORTAL_BASE = import.meta.env.VITE_PORTAL_API_BASE ?? 'https://localhost:7003'

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

function parseJsonObject(rawJson?: string): Record<string, unknown> {
  if (!rawJson || !rawJson.trim()) {
    return {}
  }

  try {
    const parsed = JSON.parse(rawJson) as unknown
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>
    }
  } catch {
    return {}
  }

  return {}
}

function readString(config: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = config[key]
    if (typeof value === 'string' && value.trim()) {
      return value.trim()
    }
  }

  return ''
}

function readBoolean(config: Record<string, unknown>, key: string, fallback = false): boolean {
  const value = config[key]
  if (typeof value === 'boolean') {
    return value
  }

  if (typeof value === 'string') {
    const normalized = value.toLowerCase()
    if (normalized === 'true') {
      return true
    }

    if (normalized === 'false') {
      return false
    }
  }

  return fallback
}

function toProviderTypeValue(value: number): ProviderTypeValue {
  if (value === 2) {
    return '2'
  }

  if (value === 3) {
    return '3'
  }

  return '1'
}

function createDefaultProviderDraft(): ProviderDraft {
  return {
    code: '',
    name: '',
    providerType: '1',
    enabled: true,
    appKey: '',
    appId: '',
    corpId: '',
    agentId: '',
    appSecret: '',
  }
}

function createDefaultSourceDraft(): SourceDraft {
  return {
    code: '',
    name: '',
    providerType: '1',
    enabled: true,
    useMock: false,
    appKey: '',
    appId: '',
    corpId: '',
    appSecret: '',
    rootDeptId: '1',
    rootDeptName: 'Root',
    strategyConfigJson: '{}',
    jobConfigJson: '{}',
  }
}

function buildProviderConfig(draft: ProviderDraft): Record<string, string> {
  if (draft.providerType === '1') {
    const config: Record<string, string> = {
      appKey: draft.appKey.trim(),
      appSecret: draft.appSecret.trim(),
    }
    if (draft.corpId.trim()) {
      config.corpId = draft.corpId.trim()
    }

    return config
  }

  if (draft.providerType === '2') {
    return {
      appId: draft.appId.trim(),
      appSecret: draft.appSecret.trim(),
    }
  }

  return {
    corpId: draft.corpId.trim(),
    agentId: draft.agentId.trim(),
    appSecret: draft.appSecret.trim(),
  }
}

function buildSourceBasicConfig(draft: SourceDraft): Record<string, unknown> {
  const config: Record<string, unknown> = {
    useMock: draft.useMock,
  }

  if (draft.useMock) {
    return config
  }

  if (draft.providerType === '1') {
    config.appKey = draft.appKey.trim()
    config.appSecret = draft.appSecret.trim()
    if (draft.rootDeptId.trim()) {
      const parsedRootDeptId = Number(draft.rootDeptId.trim())
      config.rootDeptId = Number.isNaN(parsedRootDeptId) ? draft.rootDeptId.trim() : parsedRootDeptId
    }
    if (draft.rootDeptName.trim()) {
      config.rootDeptName = draft.rootDeptName.trim()
    }
    return config
  }

  if (draft.providerType === '2') {
    config.appId = draft.appId.trim()
    config.appSecret = draft.appSecret.trim()
    return config
  }

  config.corpId = draft.corpId.trim()
  config.appSecret = draft.appSecret.trim()
  return config
}

function isJsonObjectText(text: string): boolean {
  try {
    const parsed = JSON.parse(text)
    return !!parsed && typeof parsed === 'object' && !Array.isArray(parsed)
  } catch {
    return false
  }
}

function getProviderPath(providerType: ProviderTypeValue): string {
  if (providerType === '2') {
    return 'feishu'
  }

  if (providerType === '3') {
    return 'wecom'
  }

  return 'dingtalk'
}

function buildProviderCallbackUrl(providerType: ProviderTypeValue, providerCode: string): string {
  const codePart = providerCode.trim() || '{provider-code}'
  return `${PORTAL_BASE}/login/${getProviderPath(providerType)}/${codePart}`
}

function providerDraftFromEntity(provider: IdentityProviderItem): ProviderDraft {
  const config = parseJsonObject(provider.configJson)
  return {
    code: provider.code,
    name: provider.name,
    providerType: toProviderTypeValue(provider.providerType),
    enabled: provider.enabled,
    appKey: readString(config, 'appKey', 'appId', 'clientId'),
    appId: readString(config, 'appId', 'clientId'),
    corpId: readString(config, 'corpId'),
    agentId: readString(config, 'agentId'),
    appSecret: readString(config, 'appSecret', 'clientSecret', 'corpSecret'),
  }
}

function sourceDraftFromEntity(source: IdentitySourceItem): SourceDraft {
  const config = parseJsonObject(source.basicConfigJson)
  const rootDeptRaw = config.rootDeptId
  const rootDeptId =
    typeof rootDeptRaw === 'number'
      ? rootDeptRaw.toString()
      : typeof rootDeptRaw === 'string'
        ? rootDeptRaw
        : '1'

  return {
    code: source.code,
    name: source.name,
    providerType: toProviderTypeValue(source.providerType),
    enabled: source.enabled,
    useMock: readBoolean(config, 'useMock', false),
    appKey: readString(config, 'appKey'),
    appId: readString(config, 'appId'),
    corpId: readString(config, 'corpId'),
    appSecret: readString(config, 'appSecret', 'corpSecret'),
    rootDeptId,
    rootDeptName: readString(config, 'rootDeptName') || 'Root',
    strategyConfigJson: source.strategyConfigJson || '{}',
    jobConfigJson: source.jobConfigJson || '{}',
  }
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

  const [providerCreate, setProviderCreate] = useState<ProviderDraft>(() => createDefaultProviderDraft())
  const [providerEdit, setProviderEdit] = useState<ProviderDraft | null>(null)
  const [selectedProviderCode, setSelectedProviderCode] = useState<string | null>(null)

  const [sourceCreate, setSourceCreate] = useState<SourceDraft>(() => createDefaultSourceDraft())
  const [sourceEdit, setSourceEdit] = useState<SourceDraft | null>(null)
  const [selectedSourceCode, setSelectedSourceCode] = useState<string | null>(null)

  const dashboardMetrics = useMemo(
    () => [
      { label: 'Users', value: users.length },
      { label: 'Identity Providers', value: providers.length },
      { label: 'Identity Sources', value: sources.length },
      { label: 'Recent Audits', value: audits.length },
    ],
    [audits.length, providers.length, sources.length, users.length],
  )

  const providerCreateCallbackUrl = useMemo(
    () => buildProviderCallbackUrl(providerCreate.providerType, providerCreate.code),
    [providerCreate.code, providerCreate.providerType],
  )

  const providerEditCallbackUrl = useMemo(() => {
    if (!providerEdit) {
      return ''
    }

    return buildProviderCallbackUrl(providerEdit.providerType, providerEdit.code)
  }, [providerEdit])

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

  useEffect(() => {
    if (!selectedProviderCode) {
      return
    }

    const selected = providers.find((provider) => provider.code === selectedProviderCode)
    if (!selected) {
      setSelectedProviderCode(null)
      setProviderEdit(null)
      return
    }

    setProviderEdit(providerDraftFromEntity(selected))
  }, [providers, selectedProviderCode])

  useEffect(() => {
    if (!selectedSourceCode) {
      return
    }

    const selected = sources.find((source) => source.code === selectedSourceCode)
    if (!selected) {
      setSelectedSourceCode(null)
      setSourceEdit(null)
      return
    }

    setSourceEdit(sourceDraftFromEntity(selected))
  }, [sources, selectedSourceCode])

  function updateProviderCreate<K extends keyof ProviderDraft>(key: K, value: ProviderDraft[K]) {
    setProviderCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateProviderEdit<K extends keyof ProviderDraft>(key: K, value: ProviderDraft[K]) {
    setProviderEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateSourceCreate<K extends keyof SourceDraft>(key: K, value: SourceDraft[K]) {
    setSourceCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateSourceEdit<K extends keyof SourceDraft>(key: K, value: SourceDraft[K]) {
    setSourceEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function selectProvider(provider: IdentityProviderItem) {
    setSelectedProviderCode(provider.code)
    setProviderEdit(providerDraftFromEntity(provider))
  }

  function selectSource(source: IdentitySourceItem) {
    setSelectedSourceCode(source.code)
    setSourceEdit(sourceDraftFromEntity(source))
  }

  function resetProviderEdit() {
    if (!selectedProviderCode) {
      return
    }

    const selected = providers.find((provider) => provider.code === selectedProviderCode)
    if (!selected) {
      return
    }

    setProviderEdit(providerDraftFromEntity(selected))
  }

  function resetSourceEdit() {
    if (!selectedSourceCode) {
      return
    }

    const selected = sources.find((source) => source.code === selectedSourceCode)
    if (!selected) {
      return
    }

    setSourceEdit(sourceDraftFromEntity(selected))
  }

  async function handleCreateProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setSuccess('')

    const code = providerCreate.code.trim()
    try {
      await requestJson('/api/admin/identity-providers', {
        method: 'POST',
        body: JSON.stringify({
          code,
          name: providerCreate.name.trim(),
          providerType: Number(providerCreate.providerType),
          enabled: providerCreate.enabled,
          configJson: JSON.stringify(buildProviderConfig(providerCreate)),
        }),
      })
      setProviderCreate(createDefaultProviderDraft())
      setSuccess('Identity Provider created.')
      await refreshAll()
      setSelectedProviderCode(code)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to create provider.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!providerEdit) {
      return
    }

    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-providers/${encodeURIComponent(providerEdit.code)}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: providerEdit.name.trim(),
          providerType: Number(providerEdit.providerType),
          enabled: providerEdit.enabled,
          configJson: JSON.stringify(buildProviderConfig(providerEdit)),
        }),
      })
      setSuccess(`Identity Provider ${providerEdit.code} updated.`)
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to update provider.')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteProvider() {
    if (!providerEdit) {
      return
    }

    if (!window.confirm(`Delete provider ${providerEdit.code}?`)) {
      return
    }

    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-providers/${encodeURIComponent(providerEdit.code)}`, {
        method: 'DELETE',
      })
      setSelectedProviderCode(null)
      setProviderEdit(null)
      setSuccess(`Identity Provider ${providerEdit.code} deleted.`)
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to delete provider.')
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setSuccess('')

    const code = sourceCreate.code.trim()
    try {
      await requestJson('/api/admin/identity-sources', {
        method: 'POST',
        body: JSON.stringify({
          code,
          name: sourceCreate.name.trim(),
          providerType: Number(sourceCreate.providerType),
          enabled: sourceCreate.enabled,
          basicConfigJson: JSON.stringify(buildSourceBasicConfig(sourceCreate)),
          strategyConfigJson: sourceCreate.strategyConfigJson,
          jobConfigJson: sourceCreate.jobConfigJson,
        }),
      })
      setSourceCreate(createDefaultSourceDraft())
      setSuccess('Identity Source created.')
      await refreshAll()
      setSelectedSourceCode(code)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to create source.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!sourceEdit) {
      return
    }

    if (!isJsonObjectText(sourceEdit.strategyConfigJson)) {
      setError('Strategy Config must be a valid JSON object.')
      return
    }

    if (!isJsonObjectText(sourceEdit.jobConfigJson)) {
      setError('Job Config must be a valid JSON object.')
      return
    }

    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-sources/${encodeURIComponent(sourceEdit.code)}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: sourceEdit.name.trim(),
          providerType: Number(sourceEdit.providerType),
          enabled: sourceEdit.enabled,
          basicConfigJson: JSON.stringify(buildSourceBasicConfig(sourceEdit)),
          strategyConfigJson: sourceEdit.strategyConfigJson,
          jobConfigJson: sourceEdit.jobConfigJson,
        }),
      })
      setSuccess(`Identity Source ${sourceEdit.code} updated.`)
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to update source.')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteSource() {
    if (!sourceEdit) {
      return
    }

    if (!window.confirm(`Delete source ${sourceEdit.code}?`)) {
      return
    }

    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-sources/${encodeURIComponent(sourceEdit.code)}`, {
        method: 'DELETE',
      })
      setSelectedSourceCode(null)
      setSourceEdit(null)
      setSuccess(`Identity Source ${sourceEdit.code} deleted.`)
      await refreshAll()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Failed to delete source.')
    } finally {
      setLoading(false)
    }
  }

  async function handleTriggerSync(sourceCode: string) {
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      await requestJson(`/api/admin/identity-sources/${encodeURIComponent(sourceCode)}/sync`, { method: 'POST' })
      setSuccess(`Sync triggered for ${sourceCode}.`)
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
            <p className="section-hint">Create in the top form, then use the detail editor for updates and deletion.</p>

            <form onSubmit={handleCreateProvider} className="config-form create-section">
              <h3>Create Provider</h3>
              <div className="inline-form">
                <input
                  placeholder="Code"
                  value={providerCreate.code}
                  onChange={(event) => updateProviderCreate('code', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={providerCreate.name}
                  onChange={(event) => updateProviderCreate('name', event.target.value)}
                  required
                />
                <select
                  value={providerCreate.providerType}
                  onChange={(event) => updateProviderCreate('providerType', event.target.value as ProviderTypeValue)}
                >
                  <option value="1">DingTalk</option>
                  <option value="2">Feishu</option>
                  <option value="3">WeCom</option>
                </select>
                <button type="submit">Create</button>
              </div>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={providerCreate.enabled}
                  onChange={(event) => updateProviderCreate('enabled', event.target.checked)}
                />
                Enabled
              </label>

              {providerCreate.providerType === '1' && (
                <div className="inline-form">
                  <input
                    placeholder="AppKey"
                    value={providerCreate.appKey}
                    onChange={(event) => updateProviderCreate('appKey', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={providerCreate.appSecret}
                    onChange={(event) => updateProviderCreate('appSecret', event.target.value)}
                    required
                  />
                  <input
                    placeholder="CorpId (optional)"
                    value={providerCreate.corpId}
                    onChange={(event) => updateProviderCreate('corpId', event.target.value)}
                  />
                </div>
              )}

              {providerCreate.providerType === '2' && (
                <div className="inline-form">
                  <input
                    placeholder="AppId"
                    value={providerCreate.appId}
                    onChange={(event) => updateProviderCreate('appId', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={providerCreate.appSecret}
                    onChange={(event) => updateProviderCreate('appSecret', event.target.value)}
                    required
                  />
                </div>
              )}

              {providerCreate.providerType === '3' && (
                <div className="inline-form">
                  <input
                    placeholder="CorpId"
                    value={providerCreate.corpId}
                    onChange={(event) => updateProviderCreate('corpId', event.target.value)}
                    required
                  />
                  <input
                    placeholder="AgentId"
                    value={providerCreate.agentId}
                    onChange={(event) => updateProviderCreate('agentId', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={providerCreate.appSecret}
                    onChange={(event) => updateProviderCreate('appSecret', event.target.value)}
                    required
                  />
                </div>
              )}

              <p className="config-note">
                Callback URL: <code>{providerCreateCallbackUrl}</code>
              </p>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Provider List</h3>
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
                    {providers.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No identity providers yet.
                        </td>
                      </tr>
                    )}
                    {providers.map((provider) => (
                      <tr
                        key={provider.id}
                        className={`clickable-row ${selectedProviderCode === provider.code ? 'active-row' : ''}`}
                        onClick={() => selectProvider(provider)}
                      >
                        <td>{provider.code}</td>
                        <td>{provider.name}</td>
                        <td>{providerMap[provider.providerType] ?? provider.providerType}</td>
                        <td>{provider.enabled ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>

              <section className="detail-panel">
                <h3>Provider Detail</h3>
                {!providerEdit && <p className="empty-hint">Select a provider from the list to edit details.</p>}

                {providerEdit && (
                  <form onSubmit={handleSaveProvider} className="config-form">
                    <div className="inline-form">
                      <input value={providerEdit.code} disabled className="readonly-input" />
                      <input
                        placeholder="Name"
                        value={providerEdit.name}
                        onChange={(event) => updateProviderEdit('name', event.target.value)}
                        required
                      />
                      <select
                        value={providerEdit.providerType}
                        onChange={(event) => updateProviderEdit('providerType', event.target.value as ProviderTypeValue)}
                      >
                        <option value="1">DingTalk</option>
                        <option value="2">Feishu</option>
                        <option value="3">WeCom</option>
                      </select>
                    </div>

                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={providerEdit.enabled}
                        onChange={(event) => updateProviderEdit('enabled', event.target.checked)}
                      />
                      Enabled
                    </label>

                    {providerEdit.providerType === '1' && (
                      <div className="inline-form">
                        <input
                          placeholder="AppKey"
                          value={providerEdit.appKey}
                          onChange={(event) => updateProviderEdit('appKey', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={providerEdit.appSecret}
                          onChange={(event) => updateProviderEdit('appSecret', event.target.value)}
                          required
                        />
                        <input
                          placeholder="CorpId (optional)"
                          value={providerEdit.corpId}
                          onChange={(event) => updateProviderEdit('corpId', event.target.value)}
                        />
                      </div>
                    )}

                    {providerEdit.providerType === '2' && (
                      <div className="inline-form">
                        <input
                          placeholder="AppId"
                          value={providerEdit.appId}
                          onChange={(event) => updateProviderEdit('appId', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={providerEdit.appSecret}
                          onChange={(event) => updateProviderEdit('appSecret', event.target.value)}
                          required
                        />
                      </div>
                    )}

                    {providerEdit.providerType === '3' && (
                      <div className="inline-form">
                        <input
                          placeholder="CorpId"
                          value={providerEdit.corpId}
                          onChange={(event) => updateProviderEdit('corpId', event.target.value)}
                          required
                        />
                        <input
                          placeholder="AgentId"
                          value={providerEdit.agentId}
                          onChange={(event) => updateProviderEdit('agentId', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={providerEdit.appSecret}
                          onChange={(event) => updateProviderEdit('appSecret', event.target.value)}
                          required
                        />
                      </div>
                    )}

                    <p className="config-note">
                      Callback URL: <code>{providerEditCallbackUrl}</code>
                    </p>

                    <pre className="json-preview">{JSON.stringify(buildProviderConfig(providerEdit), null, 2)}</pre>

                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button type="button" className="secondary-btn" onClick={resetProviderEdit}>
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteProvider()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'sources' && (
          <div className="panel">
            <h2>Identity Sources</h2>
            <p className="section-hint">Use detail editor for full maintenance (enabled, sync policy JSON, deletion).</p>

            <form onSubmit={handleCreateSource} className="config-form create-section">
              <h3>Create Source</h3>
              <div className="inline-form">
                <input
                  placeholder="Code"
                  value={sourceCreate.code}
                  onChange={(event) => updateSourceCreate('code', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={sourceCreate.name}
                  onChange={(event) => updateSourceCreate('name', event.target.value)}
                  required
                />
                <select
                  value={sourceCreate.providerType}
                  onChange={(event) => updateSourceCreate('providerType', event.target.value as ProviderTypeValue)}
                >
                  <option value="1">DingTalk</option>
                  <option value="2">Feishu</option>
                  <option value="3">WeCom</option>
                </select>
                <button type="submit">Create</button>
              </div>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={sourceCreate.enabled}
                  onChange={(event) => updateSourceCreate('enabled', event.target.checked)}
                />
                Enabled
              </label>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={sourceCreate.useMock}
                  onChange={(event) => updateSourceCreate('useMock', event.target.checked)}
                />
                Use mock data (skip real API credential validation)
              </label>

              {!sourceCreate.useMock && sourceCreate.providerType === '1' && (
                <div className="inline-form">
                  <input
                    placeholder="AppKey"
                    value={sourceCreate.appKey}
                    onChange={(event) => updateSourceCreate('appKey', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={sourceCreate.appSecret}
                    onChange={(event) => updateSourceCreate('appSecret', event.target.value)}
                    required
                  />
                  <input
                    placeholder="Root Dept Id (default 1)"
                    value={sourceCreate.rootDeptId}
                    onChange={(event) => updateSourceCreate('rootDeptId', event.target.value)}
                  />
                  <input
                    placeholder="Root Dept Name (optional)"
                    value={sourceCreate.rootDeptName}
                    onChange={(event) => updateSourceCreate('rootDeptName', event.target.value)}
                  />
                </div>
              )}

              {!sourceCreate.useMock && sourceCreate.providerType === '2' && (
                <div className="inline-form">
                  <input
                    placeholder="AppId"
                    value={sourceCreate.appId}
                    onChange={(event) => updateSourceCreate('appId', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={sourceCreate.appSecret}
                    onChange={(event) => updateSourceCreate('appSecret', event.target.value)}
                    required
                  />
                </div>
              )}

              {!sourceCreate.useMock && sourceCreate.providerType === '3' && (
                <div className="inline-form">
                  <input
                    placeholder="CorpId"
                    value={sourceCreate.corpId}
                    onChange={(event) => updateSourceCreate('corpId', event.target.value)}
                    required
                  />
                  <input
                    type="password"
                    placeholder="AppSecret"
                    value={sourceCreate.appSecret}
                    onChange={(event) => updateSourceCreate('appSecret', event.target.value)}
                    required
                  />
                </div>
              )}
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Source List</h3>
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
                    {sources.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No identity sources yet.
                        </td>
                      </tr>
                    )}
                    {sources.map((source) => (
                      <tr
                        key={source.id}
                        className={`clickable-row ${selectedSourceCode === source.code ? 'active-row' : ''}`}
                        onClick={() => selectSource(source)}
                      >
                        <td>{source.code}</td>
                        <td>{source.name}</td>
                        <td>{providerMap[source.providerType] ?? source.providerType}</td>
                        <td>{source.enabled ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>

              <section className="detail-panel">
                <h3>Source Detail</h3>
                {!sourceEdit && <p className="empty-hint">Select a source from the list to edit details.</p>}

                {sourceEdit && (
                  <form onSubmit={handleSaveSource} className="config-form">
                    <div className="inline-form">
                      <input value={sourceEdit.code} disabled className="readonly-input" />
                      <input
                        placeholder="Name"
                        value={sourceEdit.name}
                        onChange={(event) => updateSourceEdit('name', event.target.value)}
                        required
                      />
                      <select
                        value={sourceEdit.providerType}
                        onChange={(event) => updateSourceEdit('providerType', event.target.value as ProviderTypeValue)}
                      >
                        <option value="1">DingTalk</option>
                        <option value="2">Feishu</option>
                        <option value="3">WeCom</option>
                      </select>
                    </div>

                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={sourceEdit.enabled}
                        onChange={(event) => updateSourceEdit('enabled', event.target.checked)}
                      />
                      Enabled
                    </label>

                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={sourceEdit.useMock}
                        onChange={(event) => updateSourceEdit('useMock', event.target.checked)}
                      />
                      Use mock data (skip real API credential validation)
                    </label>

                    {!sourceEdit.useMock && sourceEdit.providerType === '1' && (
                      <div className="inline-form">
                        <input
                          placeholder="AppKey"
                          value={sourceEdit.appKey}
                          onChange={(event) => updateSourceEdit('appKey', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={sourceEdit.appSecret}
                          onChange={(event) => updateSourceEdit('appSecret', event.target.value)}
                          required
                        />
                        <input
                          placeholder="Root Dept Id (default 1)"
                          value={sourceEdit.rootDeptId}
                          onChange={(event) => updateSourceEdit('rootDeptId', event.target.value)}
                        />
                        <input
                          placeholder="Root Dept Name (optional)"
                          value={sourceEdit.rootDeptName}
                          onChange={(event) => updateSourceEdit('rootDeptName', event.target.value)}
                        />
                      </div>
                    )}

                    {!sourceEdit.useMock && sourceEdit.providerType === '2' && (
                      <div className="inline-form">
                        <input
                          placeholder="AppId"
                          value={sourceEdit.appId}
                          onChange={(event) => updateSourceEdit('appId', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={sourceEdit.appSecret}
                          onChange={(event) => updateSourceEdit('appSecret', event.target.value)}
                          required
                        />
                      </div>
                    )}

                    {!sourceEdit.useMock && sourceEdit.providerType === '3' && (
                      <div className="inline-form">
                        <input
                          placeholder="CorpId"
                          value={sourceEdit.corpId}
                          onChange={(event) => updateSourceEdit('corpId', event.target.value)}
                          required
                        />
                        <input
                          type="password"
                          placeholder="AppSecret"
                          value={sourceEdit.appSecret}
                          onChange={(event) => updateSourceEdit('appSecret', event.target.value)}
                          required
                        />
                      </div>
                    )}

                    <label className="field-label" htmlFor="strategy-config">
                      Strategy Config (JSON object)
                    </label>
                    <textarea
                      id="strategy-config"
                      className="json-editor"
                      rows={4}
                      value={sourceEdit.strategyConfigJson}
                      onChange={(event) => updateSourceEdit('strategyConfigJson', event.target.value)}
                    />

                    <label className="field-label" htmlFor="job-config">
                      Job Config (JSON object)
                    </label>
                    <textarea
                      id="job-config"
                      className="json-editor"
                      rows={4}
                      value={sourceEdit.jobConfigJson}
                      onChange={(event) => updateSourceEdit('jobConfigJson', event.target.value)}
                    />

                    <pre className="json-preview">{JSON.stringify(buildSourceBasicConfig(sourceEdit), null, 2)}</pre>

                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button type="button" className="secondary-btn" onClick={resetSourceEdit}>
                        Reset
                      </button>
                      <button type="button" className="info-btn" onClick={() => void handleTriggerSync(sourceEdit.code)}>
                        Sync now
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteSource()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
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
