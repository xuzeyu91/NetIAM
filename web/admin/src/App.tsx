import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import type { RequestContext } from './adminClient'
import { createInitialRequestContext, persistRequestContext, requestJson } from './adminClient'
import './App.css'

type AdminTab =
  | 'tenants'
  | 'users'
  | 'userGroups'
  | 'organizations'
  | 'apps'
  | 'accessPolicies'
  | 'security'
  | 'settings'
  | 'monitor'
  | 'providers'
  | 'sources'
  | 'rbac'
  | 'saml'
  | 'scim'
  | 'audit'

type ProviderTypeValue = '1' | '2' | '3'
type PermissionGrantEffectValue = '1' | '2'
type SamlBindingTypeValue = '1' | '2'
type SubjectTypeValue = '1' | '2' | '3'

type TenantItem = {
  id: string
  identifier: string
  name: string
  isActive: boolean
  defaultDomain?: string
}

type UserItem = {
  id: string
  userName: string
  displayName: string
  email?: string
  phoneNumber?: string
  externalId?: string
  lockoutEnd?: string
  accessFailedCount: number
  createTime?: string
  updateTime?: string
}

type OrganizationItem = {
  id: string
  name: string
  code: string
  parentId?: string
  path: string
  displayPath: string
}

type AppItem = {
  id: string
  code: string
  name: string
  protocol: string
  enabled: boolean
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

type IdentitySourceSyncHistoryItem = {
  id: string
  triggerMode: string
  status: number
  totalUsers: number
  createdUsers: number
  updatedUsers: number
  deletedUsers: number
  skippedUsers: number
  errorMessage?: string
  startedTime: string
  endedTime?: string
}

type IdentitySourceSyncRecordItem = {
  id: string
  syncHistoryId: string
  objectType: string
  objectId: string
  action: string
  result: string
  detail?: string
  createTime?: string
  updateTime?: string
}

type PermissionItem = {
  id: string
  code: string
  name: string
  resource: string
  action: string
  description?: string
}

type RoleItem = {
  id: string
  name?: string | null
}

type UserGrantItem = {
  id: string
  code: string
  effect: number
  createTime?: string
  updateTime?: string
}

type SamlServiceProviderItem = {
  id: string
  code: string
  name: string
  entityId: string
  assertionConsumerServiceUrl: string
  singleLogoutServiceUrl?: string
  nameIdFormat: string
  audience?: string
  relayStateDefault?: string
  wantSignedAssertions: boolean
  allowUnsolicitedResponse: boolean
  bindingType: number
  enabled: boolean
  signingCertificatePem?: string
}

type ScimTokenItem = {
  id: string
  name: string
  isActive: boolean
  expiresTime?: string
  lastUsedTime?: string
  createTime?: string
}

type UserGroupItem = {
  id: string
  name: string
  description?: string
  memberCount: number
  createTime?: string
  updateTime?: string
}

type UserGroupMemberItem = {
  id: string
  userName: string
  displayName: string
  email?: string
  phoneNumber?: string
}

type AppAccessPolicyItem = {
  id: string
  appId: string
  appCode?: string
  appName?: string
  subjectType: number
  subjectId: string
  subjectName?: string
  allowAccess: boolean
  createTime?: string
  updateTime?: string
}

type SecurityBasicSettings = {
  sessionTimeoutMinutes: number
  sessionMaximum: number
  rememberMeEnabled: boolean
  rememberMeDays: number
  captchaEnabled: boolean
  captchaTtlSeconds: number
  allowMultipleSessions: boolean
}

type PasswordPolicySettings = {
  requiredLength: number
  requireDigit: boolean
  requireLowercase: boolean
  requireUppercase: boolean
  requireNonAlphanumeric: boolean
  passwordHistoryCount: number
  passwordMaxAgeDays: number
  enableWeakPasswordCheck: boolean
}

type DefensePolicySettings = {
  maxFailedAttempts: number
  lockoutMinutes: number
  autoUnlockMinutes: number
  enableIpRateLimit: boolean
  enableRiskAudit: boolean
}

type AdministratorItem = {
  id: string
  userName: string
  displayName: string
  email?: string
  phoneNumber?: string
}

type MessageSettings = {
  emailProvider: string
  emailFromAddress: string
  emailHost: string
  emailPort: number
  emailUseSsl: boolean
  smsProvider: string
  smsSignName: string
  smsTemplateCode: string
  mailTemplate: string
  smsTemplate: string
}

type StorageSettings = {
  provider: string
  endpoint: string
  bucket: string
  accessKeyId: string
  secretAccessKey: string
  region: string
}

type GeoIpSettings = {
  enabled: boolean
  provider: string
  databasePath: string
  apiEndpoint: string
  apiToken: string
}

type MonitorSessionItem = {
  sessionId: string
  userId?: string
  userName?: string
  eventType: string
  resultStatus: string
  ipAddress?: string
  userAgent?: string
  occurredTime: string
  revoked: boolean
}

type AuditItem = {
  id: string
  eventType: string
  content: string
  resultStatus: string
  occurredTime: string
}

type TenantDraft = {
  identifier: string
  name: string
  defaultDomain: string
  isActive: boolean
}

type UserCreateDraft = {
  username: string
  displayName: string
  password: string
  email: string
  phoneNumber: string
  externalId: string
}

type UserEditDraft = {
  displayName: string
  email: string
  phoneNumber: string
  externalId: string
}

type OrganizationDraft = {
  name: string
  code: string
  parentId: string
}

type AppCreateDraft = {
  code: string
  name: string
  protocol: string
}

type AppEditDraft = {
  name: string
  protocol: string
  enabled: boolean
}

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

type PermissionCreateDraft = {
  code: string
  name: string
  resource: string
  action: string
  description: string
}

type RolePermissionDraft = {
  roleName: string
  permissionCode: string
}

type UserGrantDraft = {
  userId: string
  permissionCode: string
  effect: PermissionGrantEffectValue
}

type SamlDraft = {
  code: string
  name: string
  entityId: string
  assertionConsumerServiceUrl: string
  singleLogoutServiceUrl: string
  nameIdFormat: string
  audience: string
  relayStateDefault: string
  wantSignedAssertions: boolean
  allowUnsolicitedResponse: boolean
  bindingType: SamlBindingTypeValue
  enabled: boolean
  signingCertificatePem: string
}

type ScimTokenDraft = {
  name: string
  expiresInDays: string
}

type UserGroupDraft = {
  name: string
  description: string
}

type AppAccessPolicyDraft = {
  appId: string
  subjectType: SubjectTypeValue
  subjectId: string
  allowAccess: boolean
}

const tabs: Array<{ id: AdminTab; label: string }> = [
  { id: 'tenants', label: 'Tenants' },
  { id: 'users', label: 'Users' },
  { id: 'userGroups', label: 'User Groups' },
  { id: 'organizations', label: 'Organizations' },
  { id: 'apps', label: 'Apps' },
  { id: 'accessPolicies', label: 'Access Policies' },
  { id: 'security', label: 'Security' },
  { id: 'settings', label: 'Settings' },
  { id: 'monitor', label: 'Monitor' },
  { id: 'providers', label: 'Identity Providers' },
  { id: 'sources', label: 'Identity Sources' },
  { id: 'rbac', label: 'RBAC' },
  { id: 'saml', label: 'SAML SP' },
  { id: 'scim', label: 'SCIM Tokens' },
  { id: 'audit', label: 'Audit' },
]

const providerMap: Record<number, string> = {
  1: 'DingTalk',
  2: 'Feishu',
  3: 'WeCom',
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

function toSamlBindingTypeValue(value: number): SamlBindingTypeValue {
  if (value === 2) {
    return '2'
  }

  return '1'
}

function toSubjectTypeValue(value: number): SubjectTypeValue {
  if (value === 2) {
    return '2'
  }

  if (value === 3) {
    return '3'
  }

  return '1'
}

function toSubjectTypeLabel(value: number): string {
  if (value === 2) {
    return 'Group'
  }

  if (value === 3) {
    return 'Organization'
  }

  return 'User'
}

function toSyncStatusLabel(value: number): string {
  if (value === 2) {
    return 'Partial'
  }

  if (value === 3) {
    return 'Failed'
  }

  return 'Success'
}

function toGrantEffectLabel(value: number): string {
  if (value === 2) {
    return 'Deny'
  }

  return 'Allow'
}

function isJsonObjectText(text: string): boolean {
  try {
    const parsed = JSON.parse(text)
    return !!parsed && typeof parsed === 'object' && !Array.isArray(parsed)
  } catch {
    return false
  }
}

function trimToNull(value: string): string | null {
  const normalized = value.trim()
  return normalized ? normalized : null
}

function formatDateTime(rawValue?: string): string {
  if (!rawValue) {
    return '-'
  }

  const parsed = new Date(rawValue)
  if (Number.isNaN(parsed.getTime())) {
    return rawValue
  }

  return parsed.toLocaleString()
}

function optionOrDash(value?: string | null): string {
  if (!value || !value.trim()) {
    return '-'
  }

  return value
}

function normalizeRoleName(role: RoleItem): string {
  return role.name?.trim() ?? ''
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

function buildProviderCallbackUrl(portalBase: string, providerType: ProviderTypeValue, providerCode: string): string {
  const normalizedPortalBase = portalBase.endsWith('/') ? portalBase.slice(0, -1) : portalBase
  const codePart = providerCode.trim() || '{provider-code}'
  return `${normalizedPortalBase}/login/${getProviderPath(providerType)}/${codePart}`
}

function createDefaultTenantDraft(): TenantDraft {
  return {
    identifier: '',
    name: '',
    defaultDomain: '',
    isActive: true,
  }
}

function tenantDraftFromEntity(tenant: TenantItem): TenantDraft {
  return {
    identifier: tenant.identifier,
    name: tenant.name,
    defaultDomain: tenant.defaultDomain ?? '',
    isActive: tenant.isActive,
  }
}

function createDefaultUserCreateDraft(): UserCreateDraft {
  return {
    username: '',
    displayName: '',
    password: '',
    email: '',
    phoneNumber: '',
    externalId: '',
  }
}

function userEditDraftFromEntity(user: UserItem): UserEditDraft {
  return {
    displayName: user.displayName,
    email: user.email ?? '',
    phoneNumber: user.phoneNumber ?? '',
    externalId: user.externalId ?? '',
  }
}

function createDefaultOrganizationDraft(): OrganizationDraft {
  return {
    name: '',
    code: '',
    parentId: '',
  }
}

function organizationDraftFromEntity(organization: OrganizationItem): OrganizationDraft {
  return {
    name: organization.name,
    code: organization.code,
    parentId: organization.parentId ?? '',
  }
}

function createDefaultAppCreateDraft(): AppCreateDraft {
  return {
    code: '',
    name: '',
    protocol: 'oidc',
  }
}

function appEditDraftFromEntity(app: AppItem): AppEditDraft {
  return {
    name: app.name,
    protocol: app.protocol,
    enabled: app.enabled,
  }
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

function createDefaultPermissionCreateDraft(): PermissionCreateDraft {
  return {
    code: '',
    name: '',
    resource: '',
    action: '',
    description: '',
  }
}

function createDefaultRolePermissionDraft(): RolePermissionDraft {
  return {
    roleName: '',
    permissionCode: '',
  }
}

function createDefaultUserGrantDraft(): UserGrantDraft {
  return {
    userId: '',
    permissionCode: '',
    effect: '1',
  }
}

function createDefaultSamlDraft(): SamlDraft {
  return {
    code: '',
    name: '',
    entityId: '',
    assertionConsumerServiceUrl: '',
    singleLogoutServiceUrl: '',
    nameIdFormat: 'urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified',
    audience: '',
    relayStateDefault: '',
    wantSignedAssertions: true,
    allowUnsolicitedResponse: false,
    bindingType: '1',
    enabled: true,
    signingCertificatePem: '',
  }
}

function samlDraftFromEntity(provider: SamlServiceProviderItem): SamlDraft {
  return {
    code: provider.code,
    name: provider.name,
    entityId: provider.entityId,
    assertionConsumerServiceUrl: provider.assertionConsumerServiceUrl,
    singleLogoutServiceUrl: provider.singleLogoutServiceUrl ?? '',
    nameIdFormat: provider.nameIdFormat,
    audience: provider.audience ?? '',
    relayStateDefault: provider.relayStateDefault ?? '',
    wantSignedAssertions: provider.wantSignedAssertions,
    allowUnsolicitedResponse: provider.allowUnsolicitedResponse,
    bindingType: toSamlBindingTypeValue(provider.bindingType),
    enabled: provider.enabled,
    signingCertificatePem: provider.signingCertificatePem ?? '',
  }
}

function buildSamlPayload(draft: SamlDraft): Record<string, unknown> {
  return {
    code: draft.code.trim(),
    name: draft.name.trim(),
    entityId: draft.entityId.trim(),
    assertionConsumerServiceUrl: draft.assertionConsumerServiceUrl.trim(),
    singleLogoutServiceUrl: trimToNull(draft.singleLogoutServiceUrl),
    nameIdFormat: draft.nameIdFormat.trim(),
    audience: trimToNull(draft.audience),
    relayStateDefault: trimToNull(draft.relayStateDefault),
    wantSignedAssertions: draft.wantSignedAssertions,
    allowUnsolicitedResponse: draft.allowUnsolicitedResponse,
    bindingType: Number(draft.bindingType),
    enabled: draft.enabled,
    signingCertificatePem: trimToNull(draft.signingCertificatePem),
  }
}

function createDefaultScimTokenDraft(): ScimTokenDraft {
  return {
    name: '',
    expiresInDays: '365',
  }
}

function createDefaultUserGroupDraft(): UserGroupDraft {
  return {
    name: '',
    description: '',
  }
}

function userGroupDraftFromEntity(group: UserGroupItem): UserGroupDraft {
  return {
    name: group.name,
    description: group.description ?? '',
  }
}

function createDefaultAppAccessPolicyDraft(appId = ''): AppAccessPolicyDraft {
  return {
    appId,
    subjectType: '1',
    subjectId: '',
    allowAccess: true,
  }
}

function appAccessPolicyDraftFromEntity(policy: AppAccessPolicyItem): AppAccessPolicyDraft {
  return {
    appId: policy.appId,
    subjectType: toSubjectTypeValue(policy.subjectType),
    subjectId: policy.subjectId,
    allowAccess: policy.allowAccess,
  }
}

function normalizeUserIdList(rawText: string): string[] {
  return rawText
    .split(/[\r\n,;]+/g)
    .map((item) => item.trim())
    .filter((item) => !!item)
    .filter((item, index, values) => values.indexOf(item) === index)
}

function createDefaultSecurityBasicSettings(): SecurityBasicSettings {
  return {
    sessionTimeoutMinutes: 480,
    sessionMaximum: 5,
    rememberMeEnabled: true,
    rememberMeDays: 14,
    captchaEnabled: false,
    captchaTtlSeconds: 300,
    allowMultipleSessions: true,
  }
}

function createDefaultPasswordPolicySettings(): PasswordPolicySettings {
  return {
    requiredLength: 12,
    requireDigit: true,
    requireLowercase: true,
    requireUppercase: true,
    requireNonAlphanumeric: true,
    passwordHistoryCount: 5,
    passwordMaxAgeDays: 90,
    enableWeakPasswordCheck: true,
  }
}

function createDefaultDefensePolicySettings(): DefensePolicySettings {
  return {
    maxFailedAttempts: 5,
    lockoutMinutes: 15,
    autoUnlockMinutes: 15,
    enableIpRateLimit: true,
    enableRiskAudit: true,
  }
}

function createDefaultMessageSettings(): MessageSettings {
  return {
    emailProvider: 'smtp',
    emailFromAddress: '',
    emailHost: '',
    emailPort: 465,
    emailUseSsl: true,
    smsProvider: 'none',
    smsSignName: '',
    smsTemplateCode: '',
    mailTemplate: '',
    smsTemplate: '',
  }
}

function createDefaultStorageSettings(): StorageSettings {
  return {
    provider: 'local',
    endpoint: '',
    bucket: '',
    accessKeyId: '',
    secretAccessKey: '',
    region: '',
  }
}

function createDefaultGeoIpSettings(): GeoIpSettings {
  return {
    enabled: false,
    provider: 'builtin',
    databasePath: '',
    apiEndpoint: '',
    apiToken: '',
  }
}

function App() {
  const [requestContext, setRequestContext] = useState<RequestContext>(() => createInitialRequestContext())
  const [activeTab, setActiveTab] = useState<AdminTab>('tenants')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const [tenants, setTenants] = useState<TenantItem[]>([])
  const [users, setUsers] = useState<UserItem[]>([])
  const [organizations, setOrganizations] = useState<OrganizationItem[]>([])
  const [apps, setApps] = useState<AppItem[]>([])
  const [providers, setProviders] = useState<IdentityProviderItem[]>([])
  const [sources, setSources] = useState<IdentitySourceItem[]>([])
  const [permissions, setPermissions] = useState<PermissionItem[]>([])
  const [roles, setRoles] = useState<RoleItem[]>([])
  const [samlProviders, setSamlProviders] = useState<SamlServiceProviderItem[]>([])
  const [scimTokens, setScimTokens] = useState<ScimTokenItem[]>([])
  const [userGroups, setUserGroups] = useState<UserGroupItem[]>([])
  const [appAccessPolicies, setAppAccessPolicies] = useState<AppAccessPolicyItem[]>([])
  const [securityBasicSettings, setSecurityBasicSettings] = useState<SecurityBasicSettings>(() =>
    createDefaultSecurityBasicSettings(),
  )
  const [passwordPolicySettings, setPasswordPolicySettings] = useState<PasswordPolicySettings>(() =>
    createDefaultPasswordPolicySettings(),
  )
  const [defensePolicySettings, setDefensePolicySettings] = useState<DefensePolicySettings>(() =>
    createDefaultDefensePolicySettings(),
  )
  const [administrators, setAdministrators] = useState<AdministratorItem[]>([])
  const [messageSettings, setMessageSettings] = useState<MessageSettings>(() => createDefaultMessageSettings())
  const [storageSettings, setStorageSettings] = useState<StorageSettings>(() => createDefaultStorageSettings())
  const [geoIpSettings, setGeoIpSettings] = useState<GeoIpSettings>(() => createDefaultGeoIpSettings())
  const [monitorSessions, setMonitorSessions] = useState<MonitorSessionItem[]>([])
  const [audits, setAudits] = useState<AuditItem[]>([])

  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null)
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [selectedUserGroupId, setSelectedUserGroupId] = useState<string | null>(null)
  const [selectedOrganizationId, setSelectedOrganizationId] = useState<string | null>(null)
  const [selectedAppId, setSelectedAppId] = useState<string | null>(null)
  const [selectedAccessPolicyId, setSelectedAccessPolicyId] = useState<string | null>(null)
  const [selectedProviderCode, setSelectedProviderCode] = useState<string | null>(null)
  const [selectedSourceCode, setSelectedSourceCode] = useState<string | null>(null)
  const [selectedSamlCode, setSelectedSamlCode] = useState<string | null>(null)
  const [newAdministratorUserId, setNewAdministratorUserId] = useState('')

  const [tenantCreate, setTenantCreate] = useState<TenantDraft>(() => createDefaultTenantDraft())
  const [tenantEdit, setTenantEdit] = useState<TenantDraft | null>(null)

  const [userCreate, setUserCreate] = useState<UserCreateDraft>(() => createDefaultUserCreateDraft())
  const [userEdit, setUserEdit] = useState<UserEditDraft | null>(null)
  const [userGroupCreate, setUserGroupCreate] = useState<UserGroupDraft>(() => createDefaultUserGroupDraft())
  const [userGroupEdit, setUserGroupEdit] = useState<UserGroupDraft | null>(null)
  const [userGroupMembers, setUserGroupMembers] = useState<UserGroupMemberItem[]>([])
  const [userGroupMemberIdsText, setUserGroupMemberIdsText] = useState('')

  const [organizationCreate, setOrganizationCreate] = useState<OrganizationDraft>(() => createDefaultOrganizationDraft())
  const [organizationEdit, setOrganizationEdit] = useState<OrganizationDraft | null>(null)

  const [appCreate, setAppCreate] = useState<AppCreateDraft>(() => createDefaultAppCreateDraft())
  const [appEdit, setAppEdit] = useState<AppEditDraft | null>(null)
  const [accessPolicyCreate, setAccessPolicyCreate] = useState<AppAccessPolicyDraft>(() => createDefaultAppAccessPolicyDraft())
  const [accessPolicyEdit, setAccessPolicyEdit] = useState<AppAccessPolicyDraft | null>(null)

  const [providerCreate, setProviderCreate] = useState<ProviderDraft>(() => createDefaultProviderDraft())
  const [providerEdit, setProviderEdit] = useState<ProviderDraft | null>(null)

  const [sourceCreate, setSourceCreate] = useState<SourceDraft>(() => createDefaultSourceDraft())
  const [sourceEdit, setSourceEdit] = useState<SourceDraft | null>(null)
  const [sourceSyncHistories, setSourceSyncHistories] = useState<IdentitySourceSyncHistoryItem[]>([])
  const [sourceSyncRecords, setSourceSyncRecords] = useState<IdentitySourceSyncRecordItem[]>([])
  const [selectedSourceHistoryId, setSelectedSourceHistoryId] = useState('')

  const [permissionCreate, setPermissionCreate] = useState<PermissionCreateDraft>(() => createDefaultPermissionCreateDraft())
  const [roleCreateName, setRoleCreateName] = useState('')
  const [rolePermissionDraft, setRolePermissionDraft] = useState<RolePermissionDraft>(() => createDefaultRolePermissionDraft())
  const [userGrantDraft, setUserGrantDraft] = useState<UserGrantDraft>(() => createDefaultUserGrantDraft())
  const [rbacInspectUserId, setRbacInspectUserId] = useState('')
  const [rbacInspectPermissions, setRbacInspectPermissions] = useState<string[]>([])
  const [rbacRoleUserId, setRbacRoleUserId] = useState('')
  const [rbacRoleName, setRbacRoleName] = useState('')
  const [rbacUserRoles, setRbacUserRoles] = useState<string[]>([])
  const [rbacGrantUserId, setRbacGrantUserId] = useState('')
  const [rbacGrantItems, setRbacGrantItems] = useState<UserGrantItem[]>([])

  const [samlCreate, setSamlCreate] = useState<SamlDraft>(() => createDefaultSamlDraft())
  const [samlEdit, setSamlEdit] = useState<SamlDraft | null>(null)

  const [scimCreate, setScimCreate] = useState<ScimTokenDraft>(() => createDefaultScimTokenDraft())
  const [generatedScimToken, setGeneratedScimToken] = useState('')

  const dashboardMetrics = useMemo(
    () => [
      { label: 'Tenants', value: tenants.length },
      { label: 'Users', value: users.length },
      { label: 'User Groups', value: userGroups.length },
      { label: 'Organizations', value: organizations.length },
      { label: 'Apps', value: apps.length },
      { label: 'Access Policies', value: appAccessPolicies.length },
      { label: 'Providers', value: providers.length },
      { label: 'Sources', value: sources.length },
      { label: 'SAML SP', value: samlProviders.length },
      { label: 'SCIM Tokens', value: scimTokens.length },
      { label: 'Audits', value: audits.length },
    ],
    [
      appAccessPolicies.length,
      apps.length,
      audits.length,
      organizations.length,
      providers.length,
      samlProviders.length,
      scimTokens.length,
      sources.length,
      tenants.length,
      userGroups.length,
      users.length,
    ],
  )

  const providerCreateCallbackUrl = useMemo(
    () => buildProviderCallbackUrl(requestContext.portalBase, providerCreate.providerType, providerCreate.code),
    [providerCreate.code, providerCreate.providerType, requestContext.portalBase],
  )

  const providerEditCallbackUrl = useMemo(() => {
    if (!providerEdit) {
      return ''
    }

    return buildProviderCallbackUrl(requestContext.portalBase, providerEdit.providerType, providerEdit.code)
  }, [providerEdit, requestContext.portalBase])

  const selectableRoleNames = useMemo(
    () =>
      roles
        .map((role) => normalizeRoleName(role))
        .filter((roleName, index, values) => roleName && values.indexOf(roleName) === index),
    [roles],
  )

  const selectablePermissionCodes = useMemo(
    () =>
      permissions
        .map((permission) => permission.code)
        .filter((code, index, values) => code && values.indexOf(code) === index),
    [permissions],
  )

  const createSubjectOptions = useMemo(() => {
    if (accessPolicyCreate.subjectType === '2') {
      return userGroups.map((group) => ({ id: group.id, label: group.name }))
    }

    if (accessPolicyCreate.subjectType === '3') {
      return organizations.map((organization) => ({ id: organization.id, label: organization.displayPath }))
    }

    return users.map((user) => ({ id: user.id, label: `${user.userName} (${user.displayName})` }))
  }, [accessPolicyCreate.subjectType, organizations, userGroups, users])

  const editSubjectOptions = useMemo(() => {
    if (!accessPolicyEdit) {
      return Array<{ id: string; label: string }>()
    }

    if (accessPolicyEdit.subjectType === '2') {
      return userGroups.map((group) => ({ id: group.id, label: group.name }))
    }

    if (accessPolicyEdit.subjectType === '3') {
      return organizations.map((organization) => ({ id: organization.id, label: organization.displayPath }))
    }

    return users.map((user) => ({ id: user.id, label: `${user.userName} (${user.displayName})` }))
  }, [accessPolicyEdit, organizations, userGroups, users])

  const administratorUserIdSet = useMemo(
    () => new Set(administrators.map((administrator) => administrator.id)),
    [administrators],
  )

  useEffect(() => {
    persistRequestContext(requestContext)
  }, [requestContext])

  async function runMutation(execute: () => Promise<void>, fallbackErrorMessage: string) {
    setLoading(true)
    setError('')
    setSuccess('')

    try {
      await execute()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : fallbackErrorMessage)
    } finally {
      setLoading(false)
    }
  }

  async function refreshAll() {
    setLoading(true)
    setError('')
    try {
      const [
        tenantData,
        userData,
        userGroupData,
        organizationData,
        appData,
        accessPolicyData,
        providerData,
        sourceData,
        permissionData,
        roleData,
        samlData,
        scimData,
        securityBasicData,
        passwordPolicyData,
        defensePolicyData,
        administratorData,
        messageSettingData,
        storageSettingData,
        geoIpSettingData,
        monitorSessionData,
        auditData,
      ] = await Promise.all([
        requestJson<TenantItem[]>(requestContext, '/api/admin/tenants'),
        requestJson<UserItem[]>(requestContext, '/api/admin/users'),
        requestJson<UserGroupItem[]>(requestContext, '/api/admin/user-groups'),
        requestJson<OrganizationItem[]>(requestContext, '/api/admin/organizations'),
        requestJson<AppItem[]>(requestContext, '/api/admin/apps'),
        requestJson<AppAccessPolicyItem[]>(requestContext, '/api/admin/app-access-policies'),
        requestJson<IdentityProviderItem[]>(requestContext, '/api/admin/identity-providers'),
        requestJson<IdentitySourceItem[]>(requestContext, '/api/admin/identity-sources'),
        requestJson<PermissionItem[]>(requestContext, '/api/admin/rbac/permissions'),
        requestJson<RoleItem[]>(requestContext, '/api/admin/rbac/roles'),
        requestJson<SamlServiceProviderItem[]>(requestContext, '/api/admin/saml/service-providers'),
        requestJson<ScimTokenItem[]>(requestContext, '/api/admin/scim/tokens'),
        requestJson<SecurityBasicSettings>(requestContext, '/api/admin/security/basic'),
        requestJson<PasswordPolicySettings>(requestContext, '/api/admin/security/password-policy'),
        requestJson<DefensePolicySettings>(requestContext, '/api/admin/security/defense-policy'),
        requestJson<AdministratorItem[]>(requestContext, '/api/admin/security/administrators'),
        requestJson<MessageSettings>(requestContext, '/api/admin/settings/message'),
        requestJson<StorageSettings>(requestContext, '/api/admin/settings/storage'),
        requestJson<GeoIpSettings>(requestContext, '/api/admin/settings/geoip'),
        requestJson<MonitorSessionItem[]>(requestContext, '/api/admin/monitor/sessions?take=100'),
        requestJson<AuditItem[]>(requestContext, '/api/admin/audit?take=100'),
      ])

      setTenants(tenantData)
      setUsers(userData)
      setUserGroups(userGroupData)
      setOrganizations(organizationData)
      setApps(appData)
      setAppAccessPolicies(accessPolicyData)
      setProviders(providerData)
      setSources(sourceData)
      setPermissions(permissionData)
      setRoles(roleData)
      setSamlProviders(samlData)
      setScimTokens(scimData)
      setSecurityBasicSettings(securityBasicData)
      setPasswordPolicySettings(passwordPolicyData)
      setDefensePolicySettings(defensePolicyData)
      setAdministrators(administratorData)
      setMessageSettings(messageSettingData)
      setStorageSettings(storageSettingData)
      setGeoIpSettings(geoIpSettingData)
      setMonitorSessions(monitorSessionData)
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
    if (!selectedTenantId) {
      setTenantEdit(null)
      return
    }

    const selected = tenants.find((tenant) => tenant.id === selectedTenantId)
    if (!selected) {
      setSelectedTenantId(null)
      setTenantEdit(null)
      return
    }

    setTenantEdit(tenantDraftFromEntity(selected))
  }, [selectedTenantId, tenants])

  useEffect(() => {
    if (!selectedUserId) {
      setUserEdit(null)
      return
    }

    const selected = users.find((user) => user.id === selectedUserId)
    if (!selected) {
      setSelectedUserId(null)
      setUserEdit(null)
      return
    }

    setUserEdit(userEditDraftFromEntity(selected))
  }, [selectedUserId, users])

  useEffect(() => {
    if (!selectedUserGroupId) {
      setUserGroupEdit(null)
      setUserGroupMembers([])
      setUserGroupMemberIdsText('')
      return
    }

    const selected = userGroups.find((group) => group.id === selectedUserGroupId)
    if (!selected) {
      setSelectedUserGroupId(null)
      setUserGroupEdit(null)
      setUserGroupMembers([])
      setUserGroupMemberIdsText('')
      return
    }

    setUserGroupEdit(userGroupDraftFromEntity(selected))
  }, [selectedUserGroupId, userGroups])

  useEffect(() => {
    if (!selectedUserGroupId) {
      return
    }

    void (async () => {
      try {
        const members = await requestJson<UserGroupMemberItem[]>(
          requestContext,
          `/api/admin/user-groups/${encodeURIComponent(selectedUserGroupId)}/members`,
        )
        setUserGroupMembers(members)
        setUserGroupMemberIdsText(members.map((member) => member.id).join('\n'))
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : 'Failed to load user group members.')
      }
    })()
  }, [requestContext, selectedUserGroupId])

  useEffect(() => {
    if (!selectedOrganizationId) {
      setOrganizationEdit(null)
      return
    }

    const selected = organizations.find((organization) => organization.id === selectedOrganizationId)
    if (!selected) {
      setSelectedOrganizationId(null)
      setOrganizationEdit(null)
      return
    }

    setOrganizationEdit(organizationDraftFromEntity(selected))
  }, [organizations, selectedOrganizationId])

  useEffect(() => {
    if (!selectedAppId) {
      setAppEdit(null)
      return
    }

    const selected = apps.find((app) => app.id === selectedAppId)
    if (!selected) {
      setSelectedAppId(null)
      setAppEdit(null)
      return
    }

    setAppEdit(appEditDraftFromEntity(selected))
  }, [apps, selectedAppId])

  useEffect(() => {
    if (!selectedAccessPolicyId) {
      setAccessPolicyEdit(null)
      return
    }

    const selected = appAccessPolicies.find((policy) => policy.id === selectedAccessPolicyId)
    if (!selected) {
      setSelectedAccessPolicyId(null)
      setAccessPolicyEdit(null)
      return
    }

    setAccessPolicyEdit(appAccessPolicyDraftFromEntity(selected))
  }, [appAccessPolicies, selectedAccessPolicyId])

  useEffect(() => {
    if (!selectedProviderCode) {
      setProviderEdit(null)
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
      setSourceEdit(null)
      setSourceSyncHistories([])
      setSourceSyncRecords([])
      setSelectedSourceHistoryId('')
      return
    }

    const selected = sources.find((source) => source.code === selectedSourceCode)
    if (!selected) {
      setSelectedSourceCode(null)
      setSourceEdit(null)
      setSourceSyncHistories([])
      setSourceSyncRecords([])
      setSelectedSourceHistoryId('')
      return
    }

    setSourceEdit(sourceDraftFromEntity(selected))
  }, [selectedSourceCode, sources])

  useEffect(() => {
    if (!selectedSamlCode) {
      setSamlEdit(null)
      return
    }

    const selected = samlProviders.find((provider) => provider.code === selectedSamlCode)
    if (!selected) {
      setSelectedSamlCode(null)
      setSamlEdit(null)
      return
    }

    setSamlEdit(samlDraftFromEntity(selected))
  }, [samlProviders, selectedSamlCode])

  useEffect(() => {
    if (!selectedSourceCode) {
      return
    }

    void (async () => {
      try {
        await loadSourceSyncHistories(selectedSourceCode)
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : 'Failed to load source sync histories.')
      }
    })()
  }, [requestContext, selectedSourceCode])

  useEffect(() => {
    if (!selectedSourceCode) {
      return
    }

    void (async () => {
      try {
        await loadSourceSyncRecords(selectedSourceCode, selectedSourceHistoryId || undefined)
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : 'Failed to load source sync records.')
      }
    })()
  }, [requestContext, selectedSourceCode, selectedSourceHistoryId])

  useEffect(() => {
    if (!rolePermissionDraft.roleName && selectableRoleNames.length > 0) {
      setRolePermissionDraft((previous) => ({ ...previous, roleName: selectableRoleNames[0] }))
    }
  }, [rolePermissionDraft.roleName, selectableRoleNames])

  useEffect(() => {
    if (!rolePermissionDraft.permissionCode && selectablePermissionCodes.length > 0) {
      setRolePermissionDraft((previous) => ({ ...previous, permissionCode: selectablePermissionCodes[0] }))
    }
  }, [rolePermissionDraft.permissionCode, selectablePermissionCodes])

  useEffect(() => {
    if (!userGrantDraft.permissionCode && selectablePermissionCodes.length > 0) {
      setUserGrantDraft((previous) => ({ ...previous, permissionCode: selectablePermissionCodes[0] }))
    }
  }, [selectablePermissionCodes, userGrantDraft.permissionCode])

  useEffect(() => {
    if (!userGrantDraft.userId && users.length > 0) {
      setUserGrantDraft((previous) => ({ ...previous, userId: users[0].id }))
    }
  }, [userGrantDraft.userId, users])

  useEffect(() => {
    if (!rbacRoleUserId && users.length > 0) {
      setRbacRoleUserId(users[0].id)
    }
  }, [rbacRoleUserId, users])

  useEffect(() => {
    if (!rbacRoleName && selectableRoleNames.length > 0) {
      setRbacRoleName(selectableRoleNames[0])
    }
  }, [rbacRoleName, selectableRoleNames])

  useEffect(() => {
    if (!rbacGrantUserId && users.length > 0) {
      setRbacGrantUserId(users[0].id)
    }
  }, [rbacGrantUserId, users])

  useEffect(() => {
    setRbacUserRoles([])
  }, [rbacRoleUserId])

  useEffect(() => {
    setRbacGrantItems([])
  }, [rbacGrantUserId])

  useEffect(() => {
    if (!newAdministratorUserId && users.length > 0) {
      setNewAdministratorUserId(users[0].id)
    }
  }, [newAdministratorUserId, users])

  useEffect(() => {
    if (!accessPolicyCreate.appId && apps.length > 0) {
      setAccessPolicyCreate((previous) => ({ ...previous, appId: apps[0].id }))
    }
  }, [accessPolicyCreate.appId, apps])

  useEffect(() => {
    if (accessPolicyCreate.subjectId) {
      return
    }

    if (accessPolicyCreate.subjectType === '1' && users.length > 0) {
      setAccessPolicyCreate((previous) => ({ ...previous, subjectId: users[0].id }))
      return
    }

    if (accessPolicyCreate.subjectType === '2' && userGroups.length > 0) {
      setAccessPolicyCreate((previous) => ({ ...previous, subjectId: userGroups[0].id }))
      return
    }

    if (accessPolicyCreate.subjectType === '3' && organizations.length > 0) {
      setAccessPolicyCreate((previous) => ({ ...previous, subjectId: organizations[0].id }))
    }
  }, [accessPolicyCreate.subjectId, accessPolicyCreate.subjectType, organizations, userGroups, users])

  function updateRequestContextField<K extends keyof RequestContext>(key: K, value: RequestContext[K]) {
    setRequestContext((previous) => ({ ...previous, [key]: value }))
  }

  function clearMessages() {
    setError('')
    setSuccess('')
    setGeneratedScimToken('')
  }

  function updateTenantCreate<K extends keyof TenantDraft>(key: K, value: TenantDraft[K]) {
    setTenantCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateTenantEdit<K extends keyof TenantDraft>(key: K, value: TenantDraft[K]) {
    setTenantEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateUserCreate<K extends keyof UserCreateDraft>(key: K, value: UserCreateDraft[K]) {
    setUserCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateUserEdit<K extends keyof UserEditDraft>(key: K, value: UserEditDraft[K]) {
    setUserEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateUserGroupCreate<K extends keyof UserGroupDraft>(key: K, value: UserGroupDraft[K]) {
    setUserGroupCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateUserGroupEdit<K extends keyof UserGroupDraft>(key: K, value: UserGroupDraft[K]) {
    setUserGroupEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateOrganizationCreate<K extends keyof OrganizationDraft>(key: K, value: OrganizationDraft[K]) {
    setOrganizationCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateOrganizationEdit<K extends keyof OrganizationDraft>(key: K, value: OrganizationDraft[K]) {
    setOrganizationEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateAppCreate<K extends keyof AppCreateDraft>(key: K, value: AppCreateDraft[K]) {
    setAppCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateAppEdit<K extends keyof AppEditDraft>(key: K, value: AppEditDraft[K]) {
    setAppEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateAccessPolicyCreate<K extends keyof AppAccessPolicyDraft>(key: K, value: AppAccessPolicyDraft[K]) {
    setAccessPolicyCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateAccessPolicyEdit<K extends keyof AppAccessPolicyDraft>(key: K, value: AppAccessPolicyDraft[K]) {
    setAccessPolicyEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateSecurityBasicSettings<K extends keyof SecurityBasicSettings>(key: K, value: SecurityBasicSettings[K]) {
    setSecurityBasicSettings((previous) => ({ ...previous, [key]: value }))
  }

  function updatePasswordPolicySettings<K extends keyof PasswordPolicySettings>(
    key: K,
    value: PasswordPolicySettings[K],
  ) {
    setPasswordPolicySettings((previous) => ({ ...previous, [key]: value }))
  }

  function updateDefensePolicySettings<K extends keyof DefensePolicySettings>(key: K, value: DefensePolicySettings[K]) {
    setDefensePolicySettings((previous) => ({ ...previous, [key]: value }))
  }

  function updateMessageSettings<K extends keyof MessageSettings>(key: K, value: MessageSettings[K]) {
    setMessageSettings((previous) => ({ ...previous, [key]: value }))
  }

  function updateStorageSettings<K extends keyof StorageSettings>(key: K, value: StorageSettings[K]) {
    setStorageSettings((previous) => ({ ...previous, [key]: value }))
  }

  function updateGeoIpSettings<K extends keyof GeoIpSettings>(key: K, value: GeoIpSettings[K]) {
    setGeoIpSettings((previous) => ({ ...previous, [key]: value }))
  }

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

  function updatePermissionCreate<K extends keyof PermissionCreateDraft>(key: K, value: PermissionCreateDraft[K]) {
    setPermissionCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateRolePermission<K extends keyof RolePermissionDraft>(key: K, value: RolePermissionDraft[K]) {
    setRolePermissionDraft((previous) => ({ ...previous, [key]: value }))
  }

  function updateUserGrant<K extends keyof UserGrantDraft>(key: K, value: UserGrantDraft[K]) {
    setUserGrantDraft((previous) => ({ ...previous, [key]: value }))
  }

  function updateSamlCreate<K extends keyof SamlDraft>(key: K, value: SamlDraft[K]) {
    setSamlCreate((previous) => ({ ...previous, [key]: value }))
  }

  function updateSamlEdit<K extends keyof SamlDraft>(key: K, value: SamlDraft[K]) {
    setSamlEdit((previous) => (previous ? { ...previous, [key]: value } : previous))
  }

  function updateScimCreate<K extends keyof ScimTokenDraft>(key: K, value: ScimTokenDraft[K]) {
    setScimCreate((previous) => ({ ...previous, [key]: value }))
  }

  async function handleCreateTenant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const createdTenant = await requestJson<TenantItem>(requestContext, '/api/admin/tenants', {
        method: 'POST',
        body: JSON.stringify({
          identifier: tenantCreate.identifier.trim(),
          name: tenantCreate.name.trim(),
          defaultDomain: trimToNull(tenantCreate.defaultDomain),
        }),
      })

      setTenantCreate(createDefaultTenantDraft())
      setSelectedTenantId(createdTenant.id)
      setSuccess(`Tenant ${createdTenant.identifier} created.`)
      await refreshAll()
    }, 'Failed to create tenant.')
  }

  async function handleSaveTenant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!tenantEdit || !selectedTenantId) {
      return
    }

    await runMutation(async () => {
      await requestJson<TenantItem>(requestContext, `/api/admin/tenants/${encodeURIComponent(selectedTenantId)}`, {
        method: 'PUT',
        body: JSON.stringify({
          identifier: tenantEdit.identifier.trim(),
          name: tenantEdit.name.trim(),
          defaultDomain: trimToNull(tenantEdit.defaultDomain),
          isActive: tenantEdit.isActive,
        }),
      })

      setSuccess(`Tenant ${tenantEdit.identifier} updated.`)
      await refreshAll()
    }, 'Failed to update tenant.')
  }

  async function handleDeleteTenant() {
    if (!tenantEdit || !selectedTenantId) {
      return
    }

    if (!window.confirm(`Delete tenant ${tenantEdit.identifier}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/tenants/${encodeURIComponent(selectedTenantId)}`, {
        method: 'DELETE',
      })

      setSelectedTenantId(null)
      setTenantEdit(null)
      setSuccess(`Tenant ${tenantEdit.identifier} deleted.`)
      await refreshAll()
    }, 'Failed to delete tenant.')
  }

  async function handleCreateUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const created = await requestJson<{ id: string; userName: string }>(requestContext, '/api/admin/users', {
        method: 'POST',
        body: JSON.stringify({
          username: userCreate.username.trim(),
          displayName: userCreate.displayName.trim(),
          password: userCreate.password,
          email: trimToNull(userCreate.email),
          phoneNumber: trimToNull(userCreate.phoneNumber),
          externalId: trimToNull(userCreate.externalId),
        }),
      })

      setUserCreate(createDefaultUserCreateDraft())
      setSelectedUserId(created.id)
      setSuccess(`User ${created.userName} created.`)
      await refreshAll()
    }, 'Failed to create user.')
  }

  async function handleSaveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!userEdit || !selectedUserId) {
      return
    }

    await runMutation(async () => {
      await requestJson<{ id: string; userName: string }>(requestContext, `/api/admin/users/${encodeURIComponent(selectedUserId)}`, {
        method: 'PUT',
        body: JSON.stringify({
          displayName: userEdit.displayName.trim(),
          email: trimToNull(userEdit.email),
          phoneNumber: trimToNull(userEdit.phoneNumber),
          externalId: trimToNull(userEdit.externalId),
        }),
      })

      setSuccess('User updated.')
      await refreshAll()
    }, 'Failed to update user.')
  }

  async function handleDeleteUser() {
    if (!userEdit || !selectedUserId) {
      return
    }

    if (!window.confirm('Delete selected user?')) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/users/${encodeURIComponent(selectedUserId)}`, {
        method: 'DELETE',
      })

      setSelectedUserId(null)
      setUserEdit(null)
      setSuccess('User deleted.')
      await refreshAll()
    }, 'Failed to delete user.')
  }

  async function handleCreateUserGroup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const created = await requestJson<UserGroupItem>(requestContext, '/api/admin/user-groups', {
        method: 'POST',
        body: JSON.stringify({
          name: userGroupCreate.name.trim(),
          description: trimToNull(userGroupCreate.description),
        }),
      })

      setUserGroupCreate(createDefaultUserGroupDraft())
      setSelectedUserGroupId(created.id)
      setSuccess(`User group ${created.name} created.`)
      await refreshAll()
    }, 'Failed to create user group.')
  }

  async function handleSaveUserGroup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!userGroupEdit || !selectedUserGroupId) {
      return
    }

    await runMutation(async () => {
      await requestJson<UserGroupItem>(requestContext, `/api/admin/user-groups/${encodeURIComponent(selectedUserGroupId)}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: userGroupEdit.name.trim(),
          description: trimToNull(userGroupEdit.description),
        }),
      })

      setSuccess(`User group ${userGroupEdit.name} updated.`)
      await refreshAll()
    }, 'Failed to update user group.')
  }

  async function handleDeleteUserGroup() {
    if (!selectedUserGroupId || !userGroupEdit) {
      return
    }

    if (!window.confirm(`Delete user group ${userGroupEdit.name}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/user-groups/${encodeURIComponent(selectedUserGroupId)}`, {
        method: 'DELETE',
      })

      setSelectedUserGroupId(null)
      setUserGroupEdit(null)
      setUserGroupMembers([])
      setUserGroupMemberIdsText('')
      setSuccess(`User group ${userGroupEdit.name} deleted.`)
      await refreshAll()
    }, 'Failed to delete user group.')
  }

  async function handleSaveUserGroupMembers() {
    if (!selectedUserGroupId) {
      return
    }

    await runMutation(async () => {
      const userIds = normalizeUserIdList(userGroupMemberIdsText)
      await requestJson<unknown>(requestContext, `/api/admin/user-groups/${encodeURIComponent(selectedUserGroupId)}/members`, {
        method: 'PUT',
        body: JSON.stringify({ userIds }),
      })

      const members = await requestJson<UserGroupMemberItem[]>(
        requestContext,
        `/api/admin/user-groups/${encodeURIComponent(selectedUserGroupId)}/members`,
      )
      setUserGroupMembers(members)
      setUserGroupMemberIdsText(members.map((member) => member.id).join('\n'))
      setSuccess('User group members updated.')
      await refreshAll()
    }, 'Failed to update user group members.')
  }

  async function handleCreateOrganization(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const created = await requestJson<OrganizationItem>(requestContext, '/api/admin/organizations', {
        method: 'POST',
        body: JSON.stringify({
          name: organizationCreate.name.trim(),
          code: organizationCreate.code.trim(),
          parentId: trimToNull(organizationCreate.parentId),
        }),
      })

      setOrganizationCreate(createDefaultOrganizationDraft())
      setSelectedOrganizationId(created.id)
      setSuccess(`Organization ${created.name} created.`)
      await refreshAll()
    }, 'Failed to create organization.')
  }

  async function handleSaveOrganization(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!organizationEdit || !selectedOrganizationId) {
      return
    }

    await runMutation(async () => {
      await requestJson<OrganizationItem>(
        requestContext,
        `/api/admin/organizations/${encodeURIComponent(selectedOrganizationId)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            name: organizationEdit.name.trim(),
            code: organizationEdit.code.trim(),
            parentId: trimToNull(organizationEdit.parentId),
          }),
        },
      )

      setSuccess(`Organization ${organizationEdit.name} updated.`)
      await refreshAll()
    }, 'Failed to update organization.')
  }

  async function handleDeleteOrganization() {
    if (!organizationEdit || !selectedOrganizationId) {
      return
    }

    if (!window.confirm(`Delete organization ${organizationEdit.name}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/organizations/${encodeURIComponent(selectedOrganizationId)}`, {
        method: 'DELETE',
      })

      setSelectedOrganizationId(null)
      setOrganizationEdit(null)
      setSuccess(`Organization ${organizationEdit.name} deleted.`)
      await refreshAll()
    }, 'Failed to delete organization.')
  }

  async function handleCreateApp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const created = await requestJson<AppItem>(requestContext, '/api/admin/apps', {
        method: 'POST',
        body: JSON.stringify({
          code: appCreate.code.trim(),
          name: appCreate.name.trim(),
          protocol: appCreate.protocol.trim(),
        }),
      })

      setAppCreate(createDefaultAppCreateDraft())
      setSelectedAppId(created.id)
      setSuccess(`App ${created.code} created.`)
      await refreshAll()
    }, 'Failed to create app.')
  }

  async function handleSaveApp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!appEdit || !selectedAppId) {
      return
    }

    await runMutation(async () => {
      await requestJson<AppItem>(requestContext, `/api/admin/apps/${encodeURIComponent(selectedAppId)}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: appEdit.name.trim(),
          protocol: appEdit.protocol.trim(),
          enabled: appEdit.enabled,
        }),
      })

      setSuccess('App updated.')
      await refreshAll()
    }, 'Failed to update app.')
  }

  async function handleDeleteApp() {
    if (!selectedAppId) {
      return
    }

    if (!window.confirm('Delete selected app?')) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/apps/${encodeURIComponent(selectedAppId)}`, {
        method: 'DELETE',
      })

      setSelectedAppId(null)
      setAppEdit(null)
      setSuccess('App deleted.')
      await refreshAll()
    }, 'Failed to delete app.')
  }

  async function handleCreateAccessPolicy(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!accessPolicyCreate.appId || !accessPolicyCreate.subjectId) {
      setError('App and subject are required.')
      return
    }

    await runMutation(async () => {
      const created = await requestJson<AppAccessPolicyItem>(requestContext, '/api/admin/app-access-policies', {
        method: 'POST',
        body: JSON.stringify({
          appId: accessPolicyCreate.appId,
          subjectType: Number(accessPolicyCreate.subjectType),
          subjectId: accessPolicyCreate.subjectId.trim(),
          allowAccess: accessPolicyCreate.allowAccess,
        }),
      })

      setSelectedAccessPolicyId(created.id)
      setAccessPolicyCreate(createDefaultAppAccessPolicyDraft(apps[0]?.id ?? ''))
      setSuccess('Access policy created.')
      await refreshAll()
    }, 'Failed to create access policy.')
  }

  async function handleSaveAccessPolicy(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedAccessPolicyId || !accessPolicyEdit) {
      return
    }

    if (!accessPolicyEdit.appId || !accessPolicyEdit.subjectId) {
      setError('App and subject are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<AppAccessPolicyItem>(
        requestContext,
        `/api/admin/app-access-policies/${encodeURIComponent(selectedAccessPolicyId)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            appId: accessPolicyEdit.appId,
            subjectType: Number(accessPolicyEdit.subjectType),
            subjectId: accessPolicyEdit.subjectId.trim(),
            allowAccess: accessPolicyEdit.allowAccess,
          }),
        },
      )

      setSuccess('Access policy updated.')
      await refreshAll()
    }, 'Failed to update access policy.')
  }

  async function handleDeleteAccessPolicy() {
    if (!selectedAccessPolicyId) {
      return
    }

    if (!window.confirm('Delete selected access policy?')) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/app-access-policies/${encodeURIComponent(selectedAccessPolicyId)}`, {
        method: 'DELETE',
      })

      setSelectedAccessPolicyId(null)
      setAccessPolicyEdit(null)
      setSuccess('Access policy deleted.')
      await refreshAll()
    }, 'Failed to delete access policy.')
  }

  async function handleSaveSecurityBasicSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<SecurityBasicSettings>(requestContext, '/api/admin/security/basic', {
        method: 'PUT',
        body: JSON.stringify(securityBasicSettings),
      })
      setSuccess('Security basic settings updated.')
      await refreshAll()
    }, 'Failed to update security basic settings.')
  }

  async function handleSavePasswordPolicySettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<PasswordPolicySettings>(requestContext, '/api/admin/security/password-policy', {
        method: 'PUT',
        body: JSON.stringify(passwordPolicySettings),
      })
      setSuccess('Password policy updated.')
      await refreshAll()
    }, 'Failed to update password policy.')
  }

  async function handleSaveDefensePolicySettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<DefensePolicySettings>(requestContext, '/api/admin/security/defense-policy', {
        method: 'PUT',
        body: JSON.stringify(defensePolicySettings),
      })
      setSuccess('Defense policy updated.')
      await refreshAll()
    }, 'Failed to update defense policy.')
  }

  async function handleAddAdministrator() {
    if (!newAdministratorUserId) {
      setError('Please select a user to grant administrator role.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/security/administrators/${encodeURIComponent(newAdministratorUserId)}`,
        { method: 'POST' },
      )
      setSuccess('Administrator granted.')
      await refreshAll()
    }, 'Failed to add administrator.')
  }

  async function handleRemoveAdministrator(administrator: AdministratorItem) {
    if (!window.confirm(`Remove administrator role from ${administrator.userName}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/security/administrators/${encodeURIComponent(administrator.id)}`,
        { method: 'DELETE' },
      )
      setSuccess(`Administrator ${administrator.userName} removed.`)
      await refreshAll()
    }, 'Failed to remove administrator.')
  }

  async function handleSaveMessageSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<MessageSettings>(requestContext, '/api/admin/settings/message', {
        method: 'PUT',
        body: JSON.stringify(messageSettings),
      })
      setSuccess('Message settings updated.')
      await refreshAll()
    }, 'Failed to update message settings.')
  }

  async function handleSaveStorageSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<StorageSettings>(requestContext, '/api/admin/settings/storage', {
        method: 'PUT',
        body: JSON.stringify(storageSettings),
      })
      setSuccess('Storage settings updated.')
      await refreshAll()
    }, 'Failed to update storage settings.')
  }

  async function handleSaveGeoIpSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runMutation(async () => {
      await requestJson<GeoIpSettings>(requestContext, '/api/admin/settings/geoip', {
        method: 'PUT',
        body: JSON.stringify(geoIpSettings),
      })
      setSuccess('GeoIP settings updated.')
      await refreshAll()
    }, 'Failed to update GeoIP settings.')
  }

  async function handleRevokeMonitorSession(session: MonitorSessionItem) {
    if (session.revoked) {
      return
    }

    if (!window.confirm(`Mark session ${session.sessionId} as revoked?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/monitor/sessions/${encodeURIComponent(session.sessionId)}/revoke`,
        { method: 'POST' },
      )
      setSuccess(`Session ${session.sessionId} marked as revoked.`)
      await refreshAll()
    }, 'Failed to revoke session.')
  }

  async function handleCreateProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const code = providerCreate.code.trim()
    await runMutation(async () => {
      await requestJson<IdentityProviderItem>(requestContext, '/api/admin/identity-providers', {
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
      setSelectedProviderCode(code)
      setSuccess(`Identity provider ${code} created.`)
      await refreshAll()
    }, 'Failed to create identity provider.')
  }

  async function handleSaveProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!providerEdit) {
      return
    }

    await runMutation(async () => {
      await requestJson<IdentityProviderItem>(
        requestContext,
        `/api/admin/identity-providers/${encodeURIComponent(providerEdit.code)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            name: providerEdit.name.trim(),
            providerType: Number(providerEdit.providerType),
            enabled: providerEdit.enabled,
            configJson: JSON.stringify(buildProviderConfig(providerEdit)),
          }),
        },
      )

      setSuccess(`Identity provider ${providerEdit.code} updated.`)
      await refreshAll()
    }, 'Failed to update identity provider.')
  }

  async function handleDeleteProvider() {
    if (!providerEdit) {
      return
    }

    if (!window.confirm(`Delete provider ${providerEdit.code}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/identity-providers/${encodeURIComponent(providerEdit.code)}`, {
        method: 'DELETE',
      })

      setSelectedProviderCode(null)
      setProviderEdit(null)
      setSuccess(`Identity provider ${providerEdit.code} deleted.`)
      await refreshAll()
    }, 'Failed to delete identity provider.')
  }

  async function handleCreateSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const code = sourceCreate.code.trim()

    if (!isJsonObjectText(sourceCreate.strategyConfigJson) || !isJsonObjectText(sourceCreate.jobConfigJson)) {
      setError('Strategy Config and Job Config must be valid JSON objects.')
      return
    }

    await runMutation(async () => {
      await requestJson<IdentitySourceItem>(requestContext, '/api/admin/identity-sources', {
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
      setSelectedSourceCode(code)
      setSuccess(`Identity source ${code} created.`)
      await refreshAll()
    }, 'Failed to create identity source.')
  }

  async function handleSaveSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!sourceEdit) {
      return
    }

    if (!isJsonObjectText(sourceEdit.strategyConfigJson) || !isJsonObjectText(sourceEdit.jobConfigJson)) {
      setError('Strategy Config and Job Config must be valid JSON objects.')
      return
    }

    await runMutation(async () => {
      await requestJson<IdentitySourceItem>(
        requestContext,
        `/api/admin/identity-sources/${encodeURIComponent(sourceEdit.code)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            name: sourceEdit.name.trim(),
            providerType: Number(sourceEdit.providerType),
            enabled: sourceEdit.enabled,
            basicConfigJson: JSON.stringify(buildSourceBasicConfig(sourceEdit)),
            strategyConfigJson: sourceEdit.strategyConfigJson,
            jobConfigJson: sourceEdit.jobConfigJson,
          }),
        },
      )

      setSuccess(`Identity source ${sourceEdit.code} updated.`)
      await refreshAll()
    }, 'Failed to update identity source.')
  }

  async function handleDeleteSource() {
    if (!sourceEdit) {
      return
    }

    if (!window.confirm(`Delete source ${sourceEdit.code}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/identity-sources/${encodeURIComponent(sourceEdit.code)}`, {
        method: 'DELETE',
      })

      setSelectedSourceCode(null)
      setSourceEdit(null)
      setSourceSyncHistories([])
      setSourceSyncRecords([])
      setSelectedSourceHistoryId('')
      setSuccess(`Identity source ${sourceEdit.code} deleted.`)
      await refreshAll()
    }, 'Failed to delete identity source.')
  }

  async function loadSourceSyncHistories(sourceCode: string) {
    const histories = await requestJson<IdentitySourceSyncHistoryItem[]>(
      requestContext,
      `/api/admin/identity-sources/${encodeURIComponent(sourceCode)}/sync-histories?take=50`,
    )
    setSourceSyncHistories(histories)
    setSelectedSourceHistoryId((previous) => {
      if (previous && histories.some((history) => history.id === previous)) {
        return previous
      }

      return histories[0]?.id ?? ''
    })
  }

  async function loadSourceSyncRecords(sourceCode: string, historyId?: string) {
    const historyQuery = historyId ? `&historyId=${encodeURIComponent(historyId)}` : ''
    const records = await requestJson<IdentitySourceSyncRecordItem[]>(
      requestContext,
      `/api/admin/identity-sources/${encodeURIComponent(sourceCode)}/sync-records?take=100${historyQuery}`,
    )
    setSourceSyncRecords(records)
  }

  async function handleTriggerSync(sourceCode: string) {
    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/identity-sources/${encodeURIComponent(sourceCode)}/sync`, {
        method: 'POST',
      })

      setSuccess(`Sync triggered for ${sourceCode}.`)
      await refreshAll()
      await loadSourceSyncHistories(sourceCode)
    }, 'Failed to trigger sync.')
  }

  async function handleCreatePermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const created = await requestJson<PermissionItem>(requestContext, '/api/admin/rbac/permissions', {
        method: 'POST',
        body: JSON.stringify({
          code: permissionCreate.code.trim(),
          name: permissionCreate.name.trim(),
          resource: permissionCreate.resource.trim(),
          action: permissionCreate.action.trim(),
          description: trimToNull(permissionCreate.description),
        }),
      })

      setPermissionCreate(createDefaultPermissionCreateDraft())
      setSuccess(`Permission ${created.code} created.`)
      await refreshAll()
    }, 'Failed to create permission.')
  }

  async function handleCreateRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const roleName = roleCreateName.trim()
      await requestJson<{ id?: string; name?: string }>(requestContext, '/api/admin/rbac/roles', {
        method: 'POST',
        body: JSON.stringify({ name: roleName }),
      })

      setRoleCreateName('')
      setSuccess(`Role ${roleName} created.`)
      await refreshAll()
    }, 'Failed to create role.')
  }

  async function handleDeleteRole(roleName: string) {
    if (!window.confirm(`Delete role ${roleName}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/rbac/roles/${encodeURIComponent(roleName)}`, {
        method: 'DELETE',
      })

      setSuccess(`Role ${roleName} deleted.`)
      await refreshAll()
    }, 'Failed to delete role.')
  }

  async function handleAssignRolePermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!rolePermissionDraft.roleName || !rolePermissionDraft.permissionCode) {
      setError('Role and permission are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<{ roleName: string; permissionCode: string }>(
        requestContext,
        `/api/admin/rbac/roles/${encodeURIComponent(rolePermissionDraft.roleName)}/permissions/${encodeURIComponent(rolePermissionDraft.permissionCode)}`,
        { method: 'POST' },
      )

      setSuccess(`Assigned ${rolePermissionDraft.permissionCode} to ${rolePermissionDraft.roleName}.`)
      await refreshAll()
    }, 'Failed to assign role permission.')
  }

  async function handleRemoveRolePermission() {
    if (!rolePermissionDraft.roleName || !rolePermissionDraft.permissionCode) {
      setError('Role and permission are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/rbac/roles/${encodeURIComponent(rolePermissionDraft.roleName)}/permissions/${encodeURIComponent(rolePermissionDraft.permissionCode)}`,
        { method: 'DELETE' },
      )

      setSuccess(`Removed ${rolePermissionDraft.permissionCode} from ${rolePermissionDraft.roleName}.`)
      await refreshAll()
    }, 'Failed to remove role permission.')
  }

  async function handleGrantUserPermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!userGrantDraft.userId || !userGrantDraft.permissionCode) {
      setError('User and permission are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/rbac/users/${encodeURIComponent(userGrantDraft.userId)}/grants`,
        {
          method: 'POST',
          body: JSON.stringify({
            permissionCode: userGrantDraft.permissionCode,
            effect: Number(userGrantDraft.effect),
          }),
        },
      )

      setSuccess(`Granted ${userGrantDraft.permissionCode} (${userGrantDraft.effect === '1' ? 'Allow' : 'Deny'}).`)
      if (rbacGrantUserId && rbacGrantUserId === userGrantDraft.userId) {
        await loadUserGrants(rbacGrantUserId)
      }
    }, 'Failed to grant user permission.')
  }

  async function loadUserRoles(userId: string) {
    const result = await requestJson<{ userId: string; roles: string[] }>(
      requestContext,
      `/api/admin/rbac/users/${encodeURIComponent(userId)}/roles`,
    )
    setRbacUserRoles(result.roles ?? [])
  }

  async function handleLoadUserRoles(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!rbacRoleUserId) {
      setError('Select a user first.')
      return
    }

    await runMutation(async () => {
      await loadUserRoles(rbacRoleUserId)
      setSuccess(`Loaded roles for user ${rbacRoleUserId}.`)
    }, 'Failed to load user roles.')
  }

  async function handleAssignUserRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!rbacRoleUserId || !rbacRoleName) {
      setError('User and role are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/rbac/users/${encodeURIComponent(rbacRoleUserId)}/roles/${encodeURIComponent(rbacRoleName)}`,
        { method: 'POST' },
      )
      await loadUserRoles(rbacRoleUserId)
      setSuccess(`Assigned role ${rbacRoleName} to ${rbacRoleUserId}.`)
    }, 'Failed to assign user role.')
  }

  async function handleRemoveUserRole(roleName: string) {
    if (!rbacRoleUserId || !roleName) {
      setError('User and role are required.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/rbac/users/${encodeURIComponent(rbacRoleUserId)}/roles/${encodeURIComponent(roleName)}`,
        { method: 'DELETE' },
      )
      await loadUserRoles(rbacRoleUserId)
      setSuccess(`Removed role ${roleName} from ${rbacRoleUserId}.`)
    }, 'Failed to remove user role.')
  }

  async function loadUserGrants(userId: string) {
    const items = await requestJson<UserGrantItem[]>(
      requestContext,
      `/api/admin/rbac/users/${encodeURIComponent(userId)}/grants`,
    )
    setRbacGrantItems(items)
  }

  async function handleLoadUserGrants(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!rbacGrantUserId) {
      setError('Select a user first.')
      return
    }

    await runMutation(async () => {
      await loadUserGrants(rbacGrantUserId)
      setSuccess(`Loaded explicit grants for ${rbacGrantUserId}.`)
    }, 'Failed to load user grants.')
  }

  async function handleRevokeUserGrant(grant: UserGrantItem) {
    if (!rbacGrantUserId) {
      setError('Select a user first.')
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(
        requestContext,
        `/api/admin/rbac/users/${encodeURIComponent(rbacGrantUserId)}/grants/${encodeURIComponent(grant.code)}?effect=${grant.effect}`,
        { method: 'DELETE' },
      )
      await loadUserGrants(rbacGrantUserId)
      setSuccess(`Revoked ${grant.code} (${toGrantEffectLabel(grant.effect)}) from ${rbacGrantUserId}.`)
    }, 'Failed to revoke user grant.')
  }

  async function handleInspectUserPermissions(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!rbacInspectUserId.trim()) {
      setError('User ID is required.')
      return
    }

    await runMutation(async () => {
      const result = await requestJson<{ userId: string; permissions: string[] }>(
        requestContext,
        `/api/admin/rbac/users/${encodeURIComponent(rbacInspectUserId.trim())}/permissions`,
      )
      setRbacInspectPermissions(result.permissions ?? [])
      setSuccess(`Loaded effective permissions for ${result.userId}.`)
    }, 'Failed to load user permissions.')
  }

  async function handleCreateSamlProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const payload = buildSamlPayload(samlCreate)
      const created = await requestJson<SamlServiceProviderItem>(requestContext, '/api/admin/saml/service-providers', {
        method: 'POST',
        body: JSON.stringify(payload),
      })

      setSamlCreate(createDefaultSamlDraft())
      setSelectedSamlCode(created.code)
      setSuccess(`SAML service provider ${created.code} upserted.`)
      await refreshAll()
    }, 'Failed to create SAML service provider.')
  }

  async function handleSaveSamlProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!samlEdit) {
      return
    }

    await runMutation(async () => {
      await requestJson<SamlServiceProviderItem>(requestContext, '/api/admin/saml/service-providers', {
        method: 'POST',
        body: JSON.stringify(buildSamlPayload(samlEdit)),
      })

      setSuccess(`SAML service provider ${samlEdit.code} updated.`)
      await refreshAll()
    }, 'Failed to update SAML service provider.')
  }

  async function handleDeleteSamlProvider() {
    if (!samlEdit) {
      return
    }

    if (!window.confirm(`Delete SAML service provider ${samlEdit.code}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/saml/service-providers/${encodeURIComponent(samlEdit.code)}`, {
        method: 'DELETE',
      })

      setSelectedSamlCode(null)
      setSamlEdit(null)
      setSuccess(`SAML service provider ${samlEdit.code} deleted.`)
      await refreshAll()
    }, 'Failed to delete SAML service provider.')
  }

  async function handleCreateScimToken(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await runMutation(async () => {
      const response = await requestJson<{ id: string; name: string; expiresTime?: string; token: string }>(
        requestContext,
        '/api/admin/scim/tokens',
        {
          method: 'POST',
          body: JSON.stringify({
            name: scimCreate.name.trim(),
            expiresInDays: Number(scimCreate.expiresInDays),
          }),
        },
      )

      setScimCreate(createDefaultScimTokenDraft())
      setGeneratedScimToken(response.token)
      setSuccess(`SCIM token ${response.name} created. Copy the token now; it will not be shown again.`)
      await refreshAll()
    }, 'Failed to create SCIM token.')
  }

  async function handleRevokeScimToken(token: ScimTokenItem) {
    if (!window.confirm(`Revoke SCIM token ${token.name}?`)) {
      return
    }

    await runMutation(async () => {
      await requestJson<unknown>(requestContext, `/api/admin/scim/tokens/${encodeURIComponent(token.id)}`, {
        method: 'DELETE',
      })

      setSuccess(`SCIM token ${token.name} revoked.`)
      await refreshAll()
    }, 'Failed to revoke SCIM token.')
  }

  return (
    <main className="layout">
      <aside className="sidebar">
        <h1>NetIAM Admin</h1>
        <p className="muted">Tenant header: {requestContext.tenantId || '-'}</p>
        {tabs.map((tab) => (
          <button key={tab.id} onClick={() => setActiveTab(tab.id)} className={activeTab === tab.id ? 'active' : ''}>
            {tab.label}
          </button>
        ))}
        <button onClick={() => void refreshAll()}>Refresh Data</button>
      </aside>

      <section className="content">
        <section className="context-panel">
          <h2>Request Context</h2>
          <p className="section-hint">
            Every request sends <code>X-Tenant-Id</code>, optional <code>X-Acting-User-Id</code>, and optional{' '}
            <code>Authorization: Bearer</code> token.
          </p>
          <div className="field-grid">
            <label className="field-block">
              <span>Admin API Base</span>
              <input
                value={requestContext.apiBase}
                onChange={(event) => updateRequestContextField('apiBase', event.target.value)}
                placeholder="https://localhost:7002"
              />
            </label>
            <label className="field-block">
              <span>Portal Base (callback preview)</span>
              <input
                value={requestContext.portalBase}
                onChange={(event) => updateRequestContextField('portalBase', event.target.value)}
                placeholder="https://localhost:7003"
              />
            </label>
            <label className="field-block">
              <span>Tenant Id</span>
              <input
                value={requestContext.tenantId}
                onChange={(event) => updateRequestContextField('tenantId', event.target.value)}
                placeholder="tenant-default"
              />
            </label>
            <label className="field-block">
              <span>Acting User Id</span>
              <input
                value={requestContext.actingUserId}
                onChange={(event) => updateRequestContextField('actingUserId', event.target.value)}
                placeholder="user-admin-default"
              />
            </label>
            <label className="field-block span-2">
              <span>Bearer Token (optional)</span>
              <input
                type="password"
                value={requestContext.bearerToken}
                onChange={(event) => updateRequestContextField('bearerToken', event.target.value)}
                placeholder="Paste access token (with or without Bearer prefix)"
              />
            </label>
          </div>
          <div className="action-row">
            <button onClick={() => void refreshAll()}>Apply Context & Refresh</button>
            <button className="secondary-btn" onClick={clearMessages}>
              Clear Messages
            </button>
          </div>
        </section>

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
        {generatedScimToken && (
          <div className="banner info">
            One-time SCIM token value: <code>{generatedScimToken}</code>
          </div>
        )}

        {activeTab === 'tenants' && (
          <div className="panel">
            <h2>Tenants</h2>
            <p className="section-hint">Manage tenant lifecycle (create, update, soft delete).</p>

            <form onSubmit={handleCreateTenant} className="config-form create-section">
              <h3>Create Tenant</h3>
              <div className="inline-form">
                <input
                  placeholder="Identifier"
                  value={tenantCreate.identifier}
                  onChange={(event) => updateTenantCreate('identifier', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={tenantCreate.name}
                  onChange={(event) => updateTenantCreate('name', event.target.value)}
                  required
                />
                <input
                  placeholder="Default Domain (optional)"
                  value={tenantCreate.defaultDomain}
                  onChange={(event) => updateTenantCreate('defaultDomain', event.target.value)}
                />
                <button type="submit">Create</button>
              </div>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={tenantCreate.isActive}
                  onChange={(event) => updateTenantCreate('isActive', event.target.checked)}
                />
                Active
              </label>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Tenant List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Identifier</th>
                      <th>Name</th>
                      <th>Active</th>
                      <th>Default Domain</th>
                    </tr>
                  </thead>
                  <tbody>
                    {tenants.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No tenants found.
                        </td>
                      </tr>
                    )}
                    {tenants.map((tenant) => (
                      <tr
                        key={tenant.id}
                        className={`clickable-row ${selectedTenantId === tenant.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedTenantId(tenant.id)}
                      >
                        <td>{tenant.identifier}</td>
                        <td>{tenant.name}</td>
                        <td>{tenant.isActive ? 'Yes' : 'No'}</td>
                        <td>{optionOrDash(tenant.defaultDomain)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="detail-panel">
                <h3>Tenant Detail</h3>
                {!tenantEdit && <p className="empty-hint">Select a tenant to edit.</p>}
                {tenantEdit && (
                  <form onSubmit={handleSaveTenant} className="config-form">
                    <div className="inline-form">
                      <input
                        placeholder="Identifier"
                        value={tenantEdit.identifier}
                        onChange={(event) => updateTenantEdit('identifier', event.target.value)}
                        required
                      />
                      <input
                        placeholder="Name"
                        value={tenantEdit.name}
                        onChange={(event) => updateTenantEdit('name', event.target.value)}
                        required
                      />
                    </div>
                    <input
                      className="long-input"
                      placeholder="Default Domain"
                      value={tenantEdit.defaultDomain}
                      onChange={(event) => updateTenantEdit('defaultDomain', event.target.value)}
                    />
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={tenantEdit.isActive}
                        onChange={(event) => updateTenantEdit('isActive', event.target.checked)}
                      />
                      Active
                    </label>
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = tenants.find((tenant) => tenant.id === selectedTenantId)
                          if (target) {
                            setTenantEdit(tenantDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteTenant()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'users' && (
          <div className="panel">
            <h2>Users</h2>
            <p className="section-hint">Full CRUD for tenant users.</p>

            <form onSubmit={handleCreateUser} className="config-form create-section">
              <h3>Create User</h3>
              <div className="inline-form">
                <input
                  placeholder="Username"
                  value={userCreate.username}
                  onChange={(event) => updateUserCreate('username', event.target.value)}
                  required
                />
                <input
                  placeholder="Display Name"
                  value={userCreate.displayName}
                  onChange={(event) => updateUserCreate('displayName', event.target.value)}
                  required
                />
                <input
                  type="password"
                  placeholder="Password"
                  value={userCreate.password}
                  onChange={(event) => updateUserCreate('password', event.target.value)}
                  required
                />
              </div>
              <div className="inline-form">
                <input
                  placeholder="Email (optional)"
                  value={userCreate.email}
                  onChange={(event) => updateUserCreate('email', event.target.value)}
                />
                <input
                  placeholder="Phone Number (optional)"
                  value={userCreate.phoneNumber}
                  onChange={(event) => updateUserCreate('phoneNumber', event.target.value)}
                />
                <input
                  placeholder="External Id (optional)"
                  value={userCreate.externalId}
                  onChange={(event) => updateUserCreate('externalId', event.target.value)}
                />
                <button type="submit">Create</button>
              </div>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>User List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Username</th>
                      <th>Display Name</th>
                      <th>Email</th>
                      <th>Failed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No users found.
                        </td>
                      </tr>
                    )}
                    {users.map((user) => (
                      <tr
                        key={user.id}
                        className={`clickable-row ${selectedUserId === user.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedUserId(user.id)}
                      >
                        <td>{user.userName}</td>
                        <td>{user.displayName}</td>
                        <td>{optionOrDash(user.email)}</td>
                        <td>{user.accessFailedCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="detail-panel">
                <h3>User Detail</h3>
                {!userEdit && <p className="empty-hint">Select a user to edit.</p>}
                {userEdit && (
                  <form onSubmit={handleSaveUser} className="config-form">
                    <input
                      className="long-input"
                      placeholder="Display Name"
                      value={userEdit.displayName}
                      onChange={(event) => updateUserEdit('displayName', event.target.value)}
                      required
                    />
                    <div className="inline-form">
                      <input
                        placeholder="Email"
                        value={userEdit.email}
                        onChange={(event) => updateUserEdit('email', event.target.value)}
                      />
                      <input
                        placeholder="Phone Number"
                        value={userEdit.phoneNumber}
                        onChange={(event) => updateUserEdit('phoneNumber', event.target.value)}
                      />
                      <input
                        placeholder="External Id"
                        value={userEdit.externalId}
                        onChange={(event) => updateUserEdit('externalId', event.target.value)}
                      />
                    </div>
                    <p className="config-note">
                      Lockout End: <code>{formatDateTime(users.find((user) => user.id === selectedUserId)?.lockoutEnd)}</code>
                    </p>
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = users.find((user) => user.id === selectedUserId)
                          if (target) {
                            setUserEdit(userEditDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteUser()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'userGroups' && (
          <div className="panel">
            <h2>User Groups</h2>
            <p className="section-hint">Manage user group metadata and member assignments.</p>

            <form onSubmit={handleCreateUserGroup} className="config-form create-section">
              <h3>Create User Group</h3>
              <div className="inline-form">
                <input
                  placeholder="Group Name"
                  value={userGroupCreate.name}
                  onChange={(event) => updateUserGroupCreate('name', event.target.value)}
                  required
                />
                <input
                  placeholder="Description (optional)"
                  value={userGroupCreate.description}
                  onChange={(event) => updateUserGroupCreate('description', event.target.value)}
                />
                <button type="submit">Create</button>
              </div>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Group List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Description</th>
                      <th>Members</th>
                    </tr>
                  </thead>
                  <tbody>
                    {userGroups.length === 0 && (
                      <tr>
                        <td colSpan={3} className="empty-cell">
                          No user groups found.
                        </td>
                      </tr>
                    )}
                    {userGroups.map((group) => (
                      <tr
                        key={group.id}
                        className={`clickable-row ${selectedUserGroupId === group.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedUserGroupId(group.id)}
                      >
                        <td>{group.name}</td>
                        <td>{optionOrDash(group.description)}</td>
                        <td>{group.memberCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>

              <section className="detail-panel">
                <h3>Group Detail</h3>
                {!userGroupEdit && <p className="empty-hint">Select a group to edit metadata and members.</p>}
                {userGroupEdit && (
                  <form onSubmit={handleSaveUserGroup} className="config-form">
                    <div className="inline-form">
                      <input
                        placeholder="Group Name"
                        value={userGroupEdit.name}
                        onChange={(event) => updateUserGroupEdit('name', event.target.value)}
                        required
                      />
                      <input
                        placeholder="Description (optional)"
                        value={userGroupEdit.description}
                        onChange={(event) => updateUserGroupEdit('description', event.target.value)}
                      />
                    </div>
                    <div className="action-row">
                      <button type="submit">Save Group</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = userGroups.find((group) => group.id === selectedUserGroupId)
                          if (target) {
                            setUserGroupEdit(userGroupDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteUserGroup()}>
                        Delete
                      </button>
                    </div>

                    <label className="field-label" htmlFor="group-member-ids">
                      Member User IDs (split by newline/comma/semicolon)
                    </label>
                    <textarea
                      id="group-member-ids"
                      className="json-editor"
                      rows={5}
                      value={userGroupMemberIdsText}
                      onChange={(event) => setUserGroupMemberIdsText(event.target.value)}
                    />

                    <div className="action-row">
                      <button type="button" className="info-btn" onClick={() => void handleSaveUserGroupMembers()}>
                        Save Members
                      </button>
                    </div>

                    <h3 className="nested-title">Resolved Members</h3>
                    <table>
                      <thead>
                        <tr>
                          <th>User ID</th>
                          <th>Username</th>
                          <th>Display Name</th>
                          <th>Email</th>
                        </tr>
                      </thead>
                      <tbody>
                        {userGroupMembers.length === 0 && (
                          <tr>
                            <td colSpan={4} className="empty-cell">
                              No resolved members.
                            </td>
                          </tr>
                        )}
                        {userGroupMembers.map((member) => (
                          <tr key={member.id}>
                            <td>{member.id}</td>
                            <td>{member.userName}</td>
                            <td>{member.displayName}</td>
                            <td>{optionOrDash(member.email)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'organizations' && (
          <div className="panel">
            <h2>Organizations</h2>
            <p className="section-hint">Manage organization tree nodes (create, update, delete).</p>

            <form onSubmit={handleCreateOrganization} className="config-form create-section">
              <h3>Create Organization</h3>
              <div className="inline-form">
                <input
                  placeholder="Code"
                  value={organizationCreate.code}
                  onChange={(event) => updateOrganizationCreate('code', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={organizationCreate.name}
                  onChange={(event) => updateOrganizationCreate('name', event.target.value)}
                  required
                />
                <select
                  value={organizationCreate.parentId}
                  onChange={(event) => updateOrganizationCreate('parentId', event.target.value)}
                >
                  <option value="">Root</option>
                  {organizations.map((organization) => (
                    <option key={organization.id} value={organization.id}>
                      {organization.displayPath}
                    </option>
                  ))}
                </select>
                <button type="submit">Create</button>
              </div>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Organization List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Name</th>
                      <th>Display Path</th>
                    </tr>
                  </thead>
                  <tbody>
                    {organizations.length === 0 && (
                      <tr>
                        <td colSpan={3} className="empty-cell">
                          No organizations found.
                        </td>
                      </tr>
                    )}
                    {organizations.map((organization) => (
                      <tr
                        key={organization.id}
                        className={`clickable-row ${selectedOrganizationId === organization.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedOrganizationId(organization.id)}
                      >
                        <td>{organization.code}</td>
                        <td>{organization.name}</td>
                        <td>{organization.displayPath}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="detail-panel">
                <h3>Organization Detail</h3>
                {!organizationEdit && <p className="empty-hint">Select an organization to edit.</p>}
                {organizationEdit && (
                  <form onSubmit={handleSaveOrganization} className="config-form">
                    <div className="inline-form">
                      <input
                        placeholder="Code"
                        value={organizationEdit.code}
                        onChange={(event) => updateOrganizationEdit('code', event.target.value)}
                        required
                      />
                      <input
                        placeholder="Name"
                        value={organizationEdit.name}
                        onChange={(event) => updateOrganizationEdit('name', event.target.value)}
                        required
                      />
                    </div>
                    <select
                      value={organizationEdit.parentId}
                      onChange={(event) => updateOrganizationEdit('parentId', event.target.value)}
                    >
                      <option value="">Root</option>
                      {organizations
                        .filter((organization) => organization.id !== selectedOrganizationId)
                        .map((organization) => (
                          <option key={organization.id} value={organization.id}>
                            {organization.displayPath}
                          </option>
                        ))}
                    </select>
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = organizations.find((organization) => organization.id === selectedOrganizationId)
                          if (target) {
                            setOrganizationEdit(organizationDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteOrganization()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'apps' && (
          <div className="panel">
            <h2>Apps</h2>
            <p className="section-hint">Manage application metadata for protocol integration.</p>

            <form onSubmit={handleCreateApp} className="config-form create-section">
              <h3>Create App</h3>
              <div className="inline-form">
                <input
                  placeholder="Code"
                  value={appCreate.code}
                  onChange={(event) => updateAppCreate('code', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={appCreate.name}
                  onChange={(event) => updateAppCreate('name', event.target.value)}
                  required
                />
                <input
                  placeholder="Protocol (oidc/jwt/form)"
                  value={appCreate.protocol}
                  onChange={(event) => updateAppCreate('protocol', event.target.value)}
                  required
                />
                <button type="submit">Create</button>
              </div>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>App List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Name</th>
                      <th>Protocol</th>
                      <th>Enabled</th>
                    </tr>
                  </thead>
                  <tbody>
                    {apps.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No applications found.
                        </td>
                      </tr>
                    )}
                    {apps.map((app) => (
                      <tr
                        key={app.id}
                        className={`clickable-row ${selectedAppId === app.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedAppId(app.id)}
                      >
                        <td>{app.code}</td>
                        <td>{app.name}</td>
                        <td>{app.protocol}</td>
                        <td>{app.enabled ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="detail-panel">
                <h3>App Detail</h3>
                {!appEdit && <p className="empty-hint">Select an app to edit.</p>}
                {appEdit && (
                  <form onSubmit={handleSaveApp} className="config-form">
                    <div className="inline-form">
                      <input
                        placeholder="Name"
                        value={appEdit.name}
                        onChange={(event) => updateAppEdit('name', event.target.value)}
                        required
                      />
                      <input
                        placeholder="Protocol"
                        value={appEdit.protocol}
                        onChange={(event) => updateAppEdit('protocol', event.target.value)}
                        required
                      />
                    </div>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={appEdit.enabled}
                        onChange={(event) => updateAppEdit('enabled', event.target.checked)}
                      />
                      Enabled
                    </label>
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = apps.find((app) => app.id === selectedAppId)
                          if (target) {
                            setAppEdit(appEditDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteApp()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'accessPolicies' && (
          <div className="panel">
            <h2>App Access Policies</h2>
            <p className="section-hint">Control user/group/organization access per application.</p>

            <form onSubmit={handleCreateAccessPolicy} className="config-form create-section">
              <h3>Create Access Policy</h3>
              <div className="inline-form">
                <select
                  value={accessPolicyCreate.appId}
                  onChange={(event) => updateAccessPolicyCreate('appId', event.target.value)}
                  required
                >
                  <option value="">Select app</option>
                  {apps.map((app) => (
                    <option key={app.id} value={app.id}>
                      {app.code} ({app.name})
                    </option>
                  ))}
                </select>
                <select
                  value={accessPolicyCreate.subjectType}
                  onChange={(event) => {
                    const subjectType = event.target.value as SubjectTypeValue
                    const fallbackSubjectId =
                      subjectType === '2'
                        ? userGroups[0]?.id ?? ''
                        : subjectType === '3'
                          ? organizations[0]?.id ?? ''
                          : users[0]?.id ?? ''
                    setAccessPolicyCreate((previous) => ({
                      ...previous,
                      subjectType,
                      subjectId: fallbackSubjectId,
                    }))
                  }}
                >
                  <option value="1">User</option>
                  <option value="2">Group</option>
                  <option value="3">Organization</option>
                </select>
                <select
                  value={accessPolicyCreate.subjectId}
                  onChange={(event) => updateAccessPolicyCreate('subjectId', event.target.value)}
                  required
                >
                  <option value="">Select subject</option>
                  {createSubjectOptions.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.label}
                    </option>
                  ))}
                </select>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={accessPolicyCreate.allowAccess}
                    onChange={(event) => updateAccessPolicyCreate('allowAccess', event.target.checked)}
                  />
                  Allow Access
                </label>
                <button type="submit">Create</button>
              </div>
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>Policy List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>App</th>
                      <th>Subject</th>
                      <th>Subject Name</th>
                      <th>Allowed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {appAccessPolicies.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No app access policies found.
                        </td>
                      </tr>
                    )}
                    {appAccessPolicies.map((policy) => (
                      <tr
                        key={policy.id}
                        className={`clickable-row ${selectedAccessPolicyId === policy.id ? 'active-row' : ''}`}
                        onClick={() => setSelectedAccessPolicyId(policy.id)}
                      >
                        <td>{policy.appCode ? `${policy.appCode} (${policy.appName ?? '-'})` : policy.appId}</td>
                        <td>
                          {toSubjectTypeLabel(policy.subjectType)} / {policy.subjectId}
                        </td>
                        <td>{optionOrDash(policy.subjectName)}</td>
                        <td>{policy.allowAccess ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>

              <section className="detail-panel">
                <h3>Policy Detail</h3>
                {!accessPolicyEdit && <p className="empty-hint">Select a policy to edit.</p>}
                {accessPolicyEdit && (
                  <form onSubmit={handleSaveAccessPolicy} className="config-form">
                    <div className="inline-form">
                      <select
                        value={accessPolicyEdit.appId}
                        onChange={(event) => updateAccessPolicyEdit('appId', event.target.value)}
                        required
                      >
                        <option value="">Select app</option>
                        {apps.map((app) => (
                          <option key={app.id} value={app.id}>
                            {app.code} ({app.name})
                          </option>
                        ))}
                      </select>
                      <select
                        value={accessPolicyEdit.subjectType}
                        onChange={(event) => {
                          const subjectType = event.target.value as SubjectTypeValue
                          const fallbackSubjectId =
                            subjectType === '2'
                              ? userGroups[0]?.id ?? ''
                              : subjectType === '3'
                                ? organizations[0]?.id ?? ''
                                : users[0]?.id ?? ''
                          setAccessPolicyEdit((previous) =>
                            previous
                              ? {
                                  ...previous,
                                  subjectType,
                                  subjectId: fallbackSubjectId,
                                }
                              : previous,
                          )
                        }}
                      >
                        <option value="1">User</option>
                        <option value="2">Group</option>
                        <option value="3">Organization</option>
                      </select>
                      <select
                        value={accessPolicyEdit.subjectId}
                        onChange={(event) => updateAccessPolicyEdit('subjectId', event.target.value)}
                        required
                      >
                        <option value="">Select subject</option>
                        {editSubjectOptions.map((option) => (
                          <option key={option.id} value={option.id}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={accessPolicyEdit.allowAccess}
                          onChange={(event) => updateAccessPolicyEdit('allowAccess', event.target.checked)}
                        />
                        Allow Access
                      </label>
                    </div>
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = appAccessPolicies.find((policy) => policy.id === selectedAccessPolicyId)
                          if (target) {
                            setAccessPolicyEdit(appAccessPolicyDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteAccessPolicy()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'security' && (
          <div className="panel">
            <h2>Security</h2>
            <p className="section-hint">Manage basic security controls, password policy, defense strategy, and administrators.</p>

            <div className="detail-layout">
              <section className="list-panel">
                <form onSubmit={handleSaveSecurityBasicSettings} className="config-form create-section">
                  <h3>Basic Security</h3>
                  <div className="inline-form">
                    <input
                      type="number"
                      min={5}
                      value={securityBasicSettings.sessionTimeoutMinutes}
                      onChange={(event) => updateSecurityBasicSettings('sessionTimeoutMinutes', Number(event.target.value))}
                      placeholder="Session Timeout (minutes)"
                      required
                    />
                    <input
                      type="number"
                      min={1}
                      value={securityBasicSettings.sessionMaximum}
                      onChange={(event) => updateSecurityBasicSettings('sessionMaximum', Number(event.target.value))}
                      placeholder="Session Maximum"
                      required
                    />
                    <input
                      type="number"
                      min={1}
                      value={securityBasicSettings.rememberMeDays}
                      onChange={(event) => updateSecurityBasicSettings('rememberMeDays', Number(event.target.value))}
                      placeholder="RememberMe Days"
                      required
                    />
                  </div>
                  <div className="inline-form">
                    <input
                      type="number"
                      min={30}
                      value={securityBasicSettings.captchaTtlSeconds}
                      onChange={(event) => updateSecurityBasicSettings('captchaTtlSeconds', Number(event.target.value))}
                      placeholder="Captcha TTL (seconds)"
                      required
                    />
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={securityBasicSettings.rememberMeEnabled}
                        onChange={(event) => updateSecurityBasicSettings('rememberMeEnabled', event.target.checked)}
                      />
                      Remember Me Enabled
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={securityBasicSettings.captchaEnabled}
                        onChange={(event) => updateSecurityBasicSettings('captchaEnabled', event.target.checked)}
                      />
                      Captcha Enabled
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={securityBasicSettings.allowMultipleSessions}
                        onChange={(event) => updateSecurityBasicSettings('allowMultipleSessions', event.target.checked)}
                      />
                      Allow Multiple Sessions
                    </label>
                    <button type="submit">Save</button>
                  </div>
                </form>

                <form onSubmit={handleSavePasswordPolicySettings} className="config-form create-section">
                  <h3>Password Policy</h3>
                  <div className="inline-form">
                    <input
                      type="number"
                      min={8}
                      value={passwordPolicySettings.requiredLength}
                      onChange={(event) => updatePasswordPolicySettings('requiredLength', Number(event.target.value))}
                      placeholder="Required Length"
                      required
                    />
                    <input
                      type="number"
                      min={0}
                      value={passwordPolicySettings.passwordHistoryCount}
                      onChange={(event) => updatePasswordPolicySettings('passwordHistoryCount', Number(event.target.value))}
                      placeholder="History Count"
                      required
                    />
                    <input
                      type="number"
                      min={0}
                      value={passwordPolicySettings.passwordMaxAgeDays}
                      onChange={(event) => updatePasswordPolicySettings('passwordMaxAgeDays', Number(event.target.value))}
                      placeholder="Max Age Days"
                      required
                    />
                  </div>
                  <div className="inline-form">
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={passwordPolicySettings.requireDigit}
                        onChange={(event) => updatePasswordPolicySettings('requireDigit', event.target.checked)}
                      />
                      Require Digit
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={passwordPolicySettings.requireLowercase}
                        onChange={(event) => updatePasswordPolicySettings('requireLowercase', event.target.checked)}
                      />
                      Require Lowercase
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={passwordPolicySettings.requireUppercase}
                        onChange={(event) => updatePasswordPolicySettings('requireUppercase', event.target.checked)}
                      />
                      Require Uppercase
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={passwordPolicySettings.requireNonAlphanumeric}
                        onChange={(event) => updatePasswordPolicySettings('requireNonAlphanumeric', event.target.checked)}
                      />
                      Require Non-alphanumeric
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={passwordPolicySettings.enableWeakPasswordCheck}
                        onChange={(event) => updatePasswordPolicySettings('enableWeakPasswordCheck', event.target.checked)}
                      />
                      Weak Password Check
                    </label>
                    <button type="submit">Save</button>
                  </div>
                </form>

                <form onSubmit={handleSaveDefensePolicySettings} className="config-form create-section">
                  <h3>Defense Policy</h3>
                  <div className="inline-form">
                    <input
                      type="number"
                      min={1}
                      value={defensePolicySettings.maxFailedAttempts}
                      onChange={(event) => updateDefensePolicySettings('maxFailedAttempts', Number(event.target.value))}
                      placeholder="Max Failed Attempts"
                      required
                    />
                    <input
                      type="number"
                      min={1}
                      value={defensePolicySettings.lockoutMinutes}
                      onChange={(event) => updateDefensePolicySettings('lockoutMinutes', Number(event.target.value))}
                      placeholder="Lockout Minutes"
                      required
                    />
                    <input
                      type="number"
                      min={1}
                      value={defensePolicySettings.autoUnlockMinutes}
                      onChange={(event) => updateDefensePolicySettings('autoUnlockMinutes', Number(event.target.value))}
                      placeholder="Auto Unlock Minutes"
                      required
                    />
                  </div>
                  <div className="inline-form">
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={defensePolicySettings.enableIpRateLimit}
                        onChange={(event) => updateDefensePolicySettings('enableIpRateLimit', event.target.checked)}
                      />
                      Enable IP Rate Limit
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={defensePolicySettings.enableRiskAudit}
                        onChange={(event) => updateDefensePolicySettings('enableRiskAudit', event.target.checked)}
                      />
                      Enable Risk Audit
                    </label>
                    <button type="submit">Save</button>
                  </div>
                </form>
              </section>

              <section className="detail-panel">
                <h3>Administrators</h3>
                <div className="inline-form">
                  <select value={newAdministratorUserId} onChange={(event) => setNewAdministratorUserId(event.target.value)}>
                    <option value="">Select user</option>
                    {users.map((user) => (
                      <option key={user.id} value={user.id}>
                        {user.userName} ({user.displayName})
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    onClick={() => void handleAddAdministrator()}
                    disabled={!newAdministratorUserId || administratorUserIdSet.has(newAdministratorUserId)}
                  >
                    Grant Administrator
                  </button>
                </div>
                <table>
                  <thead>
                    <tr>
                      <th>Username</th>
                      <th>Display Name</th>
                      <th>Email</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {administrators.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No administrators found.
                        </td>
                      </tr>
                    )}
                    {administrators.map((administrator) => (
                      <tr key={administrator.id}>
                        <td>{administrator.userName}</td>
                        <td>{administrator.displayName}</td>
                        <td>{optionOrDash(administrator.email)}</td>
                        <td>
                          <button
                            className="danger-btn"
                            onClick={() => void handleRemoveAdministrator(administrator)}
                            disabled={administrators.length <= 1}
                          >
                            Remove
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <p className="config-note">At least one administrator must remain in the tenant.</p>
              </section>
            </div>
          </div>
        )}

        {activeTab === 'settings' && (
          <div className="panel">
            <h2>System Settings</h2>
            <p className="section-hint">Configure message delivery, storage backend, and GeoIP source.</p>

            <div className="detail-layout">
              <section className="list-panel">
                <form onSubmit={handleSaveMessageSettings} className="config-form create-section">
                  <h3>Message Settings</h3>
                  <div className="inline-form">
                    <input
                      placeholder="Email Provider"
                      value={messageSettings.emailProvider}
                      onChange={(event) => updateMessageSettings('emailProvider', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Email From Address"
                      value={messageSettings.emailFromAddress}
                      onChange={(event) => updateMessageSettings('emailFromAddress', event.target.value)}
                    />
                    <input
                      placeholder="Email Host"
                      value={messageSettings.emailHost}
                      onChange={(event) => updateMessageSettings('emailHost', event.target.value)}
                    />
                    <input
                      type="number"
                      min={1}
                      value={messageSettings.emailPort}
                      onChange={(event) => updateMessageSettings('emailPort', Number(event.target.value))}
                    />
                  </div>
                  <div className="inline-form">
                    <input
                      placeholder="SMS Provider"
                      value={messageSettings.smsProvider}
                      onChange={(event) => updateMessageSettings('smsProvider', event.target.value)}
                    />
                    <input
                      placeholder="SMS Sign Name"
                      value={messageSettings.smsSignName}
                      onChange={(event) => updateMessageSettings('smsSignName', event.target.value)}
                    />
                    <input
                      placeholder="SMS Template Code"
                      value={messageSettings.smsTemplateCode}
                      onChange={(event) => updateMessageSettings('smsTemplateCode', event.target.value)}
                    />
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={messageSettings.emailUseSsl}
                        onChange={(event) => updateMessageSettings('emailUseSsl', event.target.checked)}
                      />
                      Email SSL
                    </label>
                    <button type="submit">Save</button>
                  </div>
                  <label className="field-label" htmlFor="mail-template">
                    Mail Template
                  </label>
                  <textarea
                    id="mail-template"
                    className="json-editor"
                    rows={3}
                    value={messageSettings.mailTemplate}
                    onChange={(event) => updateMessageSettings('mailTemplate', event.target.value)}
                  />
                  <label className="field-label" htmlFor="sms-template">
                    SMS Template
                  </label>
                  <textarea
                    id="sms-template"
                    className="json-editor"
                    rows={3}
                    value={messageSettings.smsTemplate}
                    onChange={(event) => updateMessageSettings('smsTemplate', event.target.value)}
                  />
                </form>
              </section>

              <section className="detail-panel">
                <form onSubmit={handleSaveStorageSettings} className="config-form create-section">
                  <h3>Storage Settings</h3>
                  <div className="inline-form">
                    <input
                      placeholder="Provider"
                      value={storageSettings.provider}
                      onChange={(event) => updateStorageSettings('provider', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Endpoint"
                      value={storageSettings.endpoint}
                      onChange={(event) => updateStorageSettings('endpoint', event.target.value)}
                    />
                    <input
                      placeholder="Bucket"
                      value={storageSettings.bucket}
                      onChange={(event) => updateStorageSettings('bucket', event.target.value)}
                    />
                  </div>
                  <div className="inline-form">
                    <input
                      placeholder="Access Key Id"
                      value={storageSettings.accessKeyId}
                      onChange={(event) => updateStorageSettings('accessKeyId', event.target.value)}
                    />
                    <input
                      type="password"
                      placeholder="Secret Access Key"
                      value={storageSettings.secretAccessKey}
                      onChange={(event) => updateStorageSettings('secretAccessKey', event.target.value)}
                    />
                    <input
                      placeholder="Region"
                      value={storageSettings.region}
                      onChange={(event) => updateStorageSettings('region', event.target.value)}
                    />
                    <button type="submit">Save</button>
                  </div>
                </form>

                <form onSubmit={handleSaveGeoIpSettings} className="config-form create-section">
                  <h3>GeoIP Settings</h3>
                  <div className="inline-form">
                    <input
                      placeholder="Provider"
                      value={geoIpSettings.provider}
                      onChange={(event) => updateGeoIpSettings('provider', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Database Path"
                      value={geoIpSettings.databasePath}
                      onChange={(event) => updateGeoIpSettings('databasePath', event.target.value)}
                    />
                    <input
                      placeholder="API Endpoint"
                      value={geoIpSettings.apiEndpoint}
                      onChange={(event) => updateGeoIpSettings('apiEndpoint', event.target.value)}
                    />
                    <input
                      type="password"
                      placeholder="API Token"
                      value={geoIpSettings.apiToken}
                      onChange={(event) => updateGeoIpSettings('apiToken', event.target.value)}
                    />
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={geoIpSettings.enabled}
                        onChange={(event) => updateGeoIpSettings('enabled', event.target.checked)}
                      />
                      Enabled
                    </label>
                    <button type="submit">Save</button>
                  </div>
                </form>
              </section>
            </div>
          </div>
        )}

        {activeTab === 'monitor' && (
          <div className="panel">
            <h2>Session Monitor</h2>
            <p className="section-hint">View recent login sessions and mark specific session identifiers as revoked.</p>
            <table>
              <thead>
                <tr>
                  <th>Session ID</th>
                  <th>User</th>
                  <th>Event</th>
                  <th>IP</th>
                  <th>Time</th>
                  <th>Revoked</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {monitorSessions.length === 0 && (
                  <tr>
                    <td colSpan={7} className="empty-cell">
                      No monitored sessions found.
                    </td>
                  </tr>
                )}
                {monitorSessions.map((session) => (
                  <tr key={`${session.sessionId}-${session.occurredTime}`}>
                    <td>{session.sessionId}</td>
                    <td>{session.userName ?? session.userId ?? '-'}</td>
                    <td>{session.eventType}</td>
                    <td>{optionOrDash(session.ipAddress)}</td>
                    <td>{formatDateTime(session.occurredTime)}</td>
                    <td>{session.revoked ? 'Yes' : 'No'}</td>
                    <td>
                      {!session.revoked && (
                        <button className="danger-btn" onClick={() => void handleRevokeMonitorSession(session)}>
                          Revoke
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'providers' && (
          <div className="panel">
            <h2>Identity Providers</h2>
            <p className="section-hint">Create in top form, then edit and delete from the detail panel.</p>

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
                        onClick={() => setSelectedProviderCode(provider.code)}
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
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = providers.find((provider) => provider.code === selectedProviderCode)
                          if (target) {
                            setProviderEdit(providerDraftFromEntity(target))
                          }
                        }}
                      >
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
            <p className="section-hint">Manage sync source config and trigger manual sync jobs.</p>

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
                    placeholder="Root Dept Id"
                    value={sourceCreate.rootDeptId}
                    onChange={(event) => updateSourceCreate('rootDeptId', event.target.value)}
                  />
                  <input
                    placeholder="Root Dept Name"
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

              <label className="field-label" htmlFor="create-strategy-config">
                Strategy Config (JSON object)
              </label>
              <textarea
                id="create-strategy-config"
                className="json-editor"
                rows={3}
                value={sourceCreate.strategyConfigJson}
                onChange={(event) => updateSourceCreate('strategyConfigJson', event.target.value)}
              />

              <label className="field-label" htmlFor="create-job-config">
                Job Config (JSON object)
              </label>
              <textarea
                id="create-job-config"
                className="json-editor"
                rows={3}
                value={sourceCreate.jobConfigJson}
                onChange={(event) => updateSourceCreate('jobConfigJson', event.target.value)}
              />
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
                        onClick={() => setSelectedSourceCode(source.code)}
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
                {!sourceEdit && <p className="empty-hint">Select a source to edit.</p>}
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
                      Use mock data
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
                          placeholder="Root Dept Id"
                          value={sourceEdit.rootDeptId}
                          onChange={(event) => updateSourceEdit('rootDeptId', event.target.value)}
                        />
                        <input
                          placeholder="Root Dept Name"
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

                    <label className="field-label" htmlFor="edit-strategy-config">
                      Strategy Config
                    </label>
                    <textarea
                      id="edit-strategy-config"
                      className="json-editor"
                      rows={4}
                      value={sourceEdit.strategyConfigJson}
                      onChange={(event) => updateSourceEdit('strategyConfigJson', event.target.value)}
                    />

                    <label className="field-label" htmlFor="edit-job-config">
                      Job Config
                    </label>
                    <textarea
                      id="edit-job-config"
                      className="json-editor"
                      rows={4}
                      value={sourceEdit.jobConfigJson}
                      onChange={(event) => updateSourceEdit('jobConfigJson', event.target.value)}
                    />

                    <pre className="json-preview">{JSON.stringify(buildSourceBasicConfig(sourceEdit), null, 2)}</pre>

                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = sources.find((source) => source.code === selectedSourceCode)
                          if (target) {
                            setSourceEdit(sourceDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="info-btn" onClick={() => void handleTriggerSync(sourceEdit.code)}>
                        Sync Now
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteSource()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
                {sourceEdit && (
                  <div className="config-form create-section">
                    <h3>Sync Histories</h3>
                    <div className="inline-form">
                      <select
                        value={selectedSourceHistoryId}
                        onChange={(event) => setSelectedSourceHistoryId(event.target.value)}
                      >
                        <option value="">All histories</option>
                        {sourceSyncHistories.map((history) => (
                          <option key={history.id} value={history.id}>
                            {`${formatDateTime(history.startedTime)} · ${history.triggerMode} · ${toSyncStatusLabel(history.status)}`}
                          </option>
                        ))}
                      </select>
                      <button type="button" onClick={() => void loadSourceSyncHistories(sourceEdit.code)}>
                        Refresh Histories
                      </button>
                    </div>
                    <table>
                      <thead>
                        <tr>
                          <th>Started</th>
                          <th>Trigger</th>
                          <th>Status</th>
                          <th>User Summary</th>
                          <th>Error</th>
                        </tr>
                      </thead>
                      <tbody>
                        {sourceSyncHistories.length === 0 && (
                          <tr>
                            <td colSpan={5} className="empty-cell">
                              No sync history yet.
                            </td>
                          </tr>
                        )}
                        {sourceSyncHistories.map((history) => (
                          <tr
                            key={history.id}
                            className={`clickable-row ${selectedSourceHistoryId === history.id ? 'active-row' : ''}`}
                            onClick={() => setSelectedSourceHistoryId(history.id)}
                          >
                            <td>{formatDateTime(history.startedTime)}</td>
                            <td>{history.triggerMode}</td>
                            <td>{toSyncStatusLabel(history.status)}</td>
                            <td>{`${history.createdUsers}/${history.updatedUsers}/${history.totalUsers}`}</td>
                            <td>{optionOrDash(history.errorMessage)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>

                    <h3 className="nested-title">Sync Records</h3>
                    <table>
                      <thead>
                        <tr>
                          <th>Time</th>
                          <th>Object</th>
                          <th>Action</th>
                          <th>Result</th>
                          <th>Detail</th>
                        </tr>
                      </thead>
                      <tbody>
                        {sourceSyncRecords.length === 0 && (
                          <tr>
                            <td colSpan={5} className="empty-cell">
                              No sync records found.
                            </td>
                          </tr>
                        )}
                        {sourceSyncRecords.map((record) => (
                          <tr key={record.id}>
                            <td>{formatDateTime(record.createTime)}</td>
                            <td>{`${record.objectType}:${record.objectId}`}</td>
                            <td>{record.action}</td>
                            <td>{record.result}</td>
                            <td title={record.detail}>{optionOrDash(record.detail?.slice(0, 120))}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'rbac' && (
          <div className="panel">
            <h2>RBAC</h2>
            <p className="section-hint">Manage permissions, roles, role bindings, and user grants.</p>
            <div className="detail-layout">
              <section className="list-panel">
                <h3>Permissions</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Name</th>
                      <th>Resource</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {permissions.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No permissions found.
                        </td>
                      </tr>
                    )}
                    {permissions.map((permission) => (
                      <tr key={permission.id}>
                        <td>{permission.code}</td>
                        <td>{permission.name}</td>
                        <td>{permission.resource}</td>
                        <td>{permission.action}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <h3 className="nested-title">Roles</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {roles.length === 0 && (
                      <tr>
                        <td colSpan={2} className="empty-cell">
                          No roles found.
                        </td>
                      </tr>
                    )}
                    {roles.map((role) => {
                      const roleName = normalizeRoleName(role)
                      return (
                        <tr key={role.id}>
                          <td>{roleName || '-'}</td>
                          <td>
                            {roleName && roleName !== 'Administrator' && (
                              <button onClick={() => void handleDeleteRole(roleName)}>Delete</button>
                            )}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </section>

              <section className="detail-panel">
                <form onSubmit={handleCreatePermission} className="config-form create-section">
                  <h3>Create Permission</h3>
                  <div className="inline-form">
                    <input
                      placeholder="Code"
                      value={permissionCreate.code}
                      onChange={(event) => updatePermissionCreate('code', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Name"
                      value={permissionCreate.name}
                      onChange={(event) => updatePermissionCreate('name', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Resource"
                      value={permissionCreate.resource}
                      onChange={(event) => updatePermissionCreate('resource', event.target.value)}
                      required
                    />
                    <input
                      placeholder="Action"
                      value={permissionCreate.action}
                      onChange={(event) => updatePermissionCreate('action', event.target.value)}
                      required
                    />
                    <button type="submit">Create</button>
                  </div>
                  <input
                    className="long-input"
                    placeholder="Description (optional)"
                    value={permissionCreate.description}
                    onChange={(event) => updatePermissionCreate('description', event.target.value)}
                  />
                </form>

                <form onSubmit={handleCreateRole} className="config-form create-section">
                  <h3>Create Role</h3>
                  <div className="inline-form">
                    <input
                      placeholder="Role Name"
                      value={roleCreateName}
                      onChange={(event) => setRoleCreateName(event.target.value)}
                      required
                    />
                    <button type="submit">Create</button>
                  </div>
                </form>

                <form onSubmit={handleAssignRolePermission} className="config-form create-section">
                  <h3>Role Permission Binding</h3>
                  <div className="inline-form">
                    <select
                      value={rolePermissionDraft.roleName}
                      onChange={(event) => updateRolePermission('roleName', event.target.value)}
                    >
                      <option value="">Select role</option>
                      {selectableRoleNames.map((roleName) => (
                        <option key={roleName} value={roleName}>
                          {roleName}
                        </option>
                      ))}
                    </select>
                    <select
                      value={rolePermissionDraft.permissionCode}
                      onChange={(event) => updateRolePermission('permissionCode', event.target.value)}
                    >
                      <option value="">Select permission</option>
                      {selectablePermissionCodes.map((permissionCode) => (
                        <option key={permissionCode} value={permissionCode}>
                          {permissionCode}
                        </option>
                      ))}
                    </select>
                    <button type="submit">Assign</button>
                    <button type="button" className="danger-btn" onClick={() => void handleRemoveRolePermission()}>
                      Remove
                    </button>
                  </div>
                </form>

                <form onSubmit={handleGrantUserPermission} className="config-form create-section">
                  <h3>User Permission Grant</h3>
                  <div className="inline-form">
                    <select value={userGrantDraft.userId} onChange={(event) => updateUserGrant('userId', event.target.value)}>
                      <option value="">Select user</option>
                      {users.map((user) => (
                        <option key={user.id} value={user.id}>
                          {user.userName}
                        </option>
                      ))}
                    </select>
                    <select
                      value={userGrantDraft.permissionCode}
                      onChange={(event) => updateUserGrant('permissionCode', event.target.value)}
                    >
                      <option value="">Select permission</option>
                      {selectablePermissionCodes.map((permissionCode) => (
                        <option key={permissionCode} value={permissionCode}>
                          {permissionCode}
                        </option>
                      ))}
                    </select>
                    <select
                      value={userGrantDraft.effect}
                      onChange={(event) => updateUserGrant('effect', event.target.value as PermissionGrantEffectValue)}
                    >
                      <option value="1">Allow</option>
                      <option value="2">Deny</option>
                    </select>
                    <button type="submit">Grant</button>
                  </div>
                </form>

                <form onSubmit={handleLoadUserRoles} className="config-form create-section">
                  <h3>User Role Bindings</h3>
                  <div className="inline-form">
                    <select value={rbacRoleUserId} onChange={(event) => setRbacRoleUserId(event.target.value)}>
                      <option value="">Select user</option>
                      {users.map((user) => (
                        <option key={user.id} value={user.id}>
                          {`${user.userName} (${user.displayName})`}
                        </option>
                      ))}
                    </select>
                    <button type="submit">Load Roles</button>
                  </div>
                </form>

                <form onSubmit={handleAssignUserRole} className="config-form create-section">
                  <div className="inline-form">
                    <select value={rbacRoleName} onChange={(event) => setRbacRoleName(event.target.value)}>
                      <option value="">Select role</option>
                      {selectableRoleNames.map((roleName) => (
                        <option key={roleName} value={roleName}>
                          {roleName}
                        </option>
                      ))}
                    </select>
                    <button type="submit">Assign Role</button>
                  </div>
                  <table>
                    <thead>
                      <tr>
                        <th>Assigned Role</th>
                        <th>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rbacUserRoles.length === 0 && (
                        <tr>
                          <td colSpan={2} className="empty-cell">
                            Load a user to inspect role bindings.
                          </td>
                        </tr>
                      )}
                      {rbacUserRoles.map((roleName) => (
                        <tr key={roleName}>
                          <td>{roleName}</td>
                          <td>
                            <button type="button" className="danger-btn" onClick={() => void handleRemoveUserRole(roleName)}>
                              Remove
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </form>

                <form onSubmit={handleLoadUserGrants} className="config-form create-section">
                  <h3>Explicit User Grants</h3>
                  <div className="inline-form">
                    <select value={rbacGrantUserId} onChange={(event) => setRbacGrantUserId(event.target.value)}>
                      <option value="">Select user</option>
                      {users.map((user) => (
                        <option key={user.id} value={user.id}>
                          {`${user.userName} (${user.displayName})`}
                        </option>
                      ))}
                    </select>
                    <button type="submit">Load Grants</button>
                  </div>
                  <table>
                    <thead>
                      <tr>
                        <th>Permission</th>
                        <th>Effect</th>
                        <th>Updated</th>
                        <th>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rbacGrantItems.length === 0 && (
                        <tr>
                          <td colSpan={4} className="empty-cell">
                            Load a user to inspect explicit grants.
                          </td>
                        </tr>
                      )}
                      {rbacGrantItems.map((grant) => (
                        <tr key={grant.id}>
                          <td>{grant.code}</td>
                          <td>{toGrantEffectLabel(grant.effect)}</td>
                          <td>{formatDateTime(grant.updateTime ?? grant.createTime)}</td>
                          <td>
                            <button type="button" className="danger-btn" onClick={() => void handleRevokeUserGrant(grant)}>
                              Revoke
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </form>

                <form onSubmit={handleInspectUserPermissions} className="config-form create-section">
                  <h3>Inspect Effective Permissions</h3>
                  <div className="inline-form">
                    <select value={rbacInspectUserId} onChange={(event) => setRbacInspectUserId(event.target.value)} required>
                      <option value="">Select user</option>
                      {users.map((user) => (
                        <option key={user.id} value={user.id}>
                          {`${user.userName} (${user.displayName})`}
                        </option>
                      ))}
                    </select>
                    <button type="submit">Load</button>
                  </div>
                  <pre className="json-preview">{JSON.stringify(rbacInspectPermissions, null, 2)}</pre>
                </form>
              </section>
            </div>
          </div>
        )}

        {activeTab === 'saml' && (
          <div className="panel">
            <h2>SAML Service Providers</h2>
            <p className="section-hint">Manage SAML SP metadata entries for SSO routing.</p>

            <form onSubmit={handleCreateSamlProvider} className="config-form create-section">
              <h3>Create / Upsert SAML SP</h3>
              <div className="inline-form">
                <input
                  placeholder="Code"
                  value={samlCreate.code}
                  onChange={(event) => updateSamlCreate('code', event.target.value)}
                  required
                />
                <input
                  placeholder="Name"
                  value={samlCreate.name}
                  onChange={(event) => updateSamlCreate('name', event.target.value)}
                  required
                />
                <input
                  placeholder="Entity ID"
                  value={samlCreate.entityId}
                  onChange={(event) => updateSamlCreate('entityId', event.target.value)}
                  required
                />
              </div>
              <div className="inline-form">
                <input
                  placeholder="ACS URL"
                  value={samlCreate.assertionConsumerServiceUrl}
                  onChange={(event) => updateSamlCreate('assertionConsumerServiceUrl', event.target.value)}
                  required
                />
                <input
                  placeholder="SLO URL (optional)"
                  value={samlCreate.singleLogoutServiceUrl}
                  onChange={(event) => updateSamlCreate('singleLogoutServiceUrl', event.target.value)}
                />
              </div>
              <div className="inline-form">
                <input
                  placeholder="NameIdFormat"
                  value={samlCreate.nameIdFormat}
                  onChange={(event) => updateSamlCreate('nameIdFormat', event.target.value)}
                />
                <input
                  placeholder="Audience (optional)"
                  value={samlCreate.audience}
                  onChange={(event) => updateSamlCreate('audience', event.target.value)}
                />
                <input
                  placeholder="RelayState default (optional)"
                  value={samlCreate.relayStateDefault}
                  onChange={(event) => updateSamlCreate('relayStateDefault', event.target.value)}
                />
              </div>
              <div className="inline-form">
                <select
                  value={samlCreate.bindingType}
                  onChange={(event) => updateSamlCreate('bindingType', event.target.value as SamlBindingTypeValue)}
                >
                  <option value="1">HTTP-POST</option>
                  <option value="2">HTTP-Redirect</option>
                </select>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={samlCreate.wantSignedAssertions}
                    onChange={(event) => updateSamlCreate('wantSignedAssertions', event.target.checked)}
                  />
                  Want Signed Assertions
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={samlCreate.allowUnsolicitedResponse}
                    onChange={(event) => updateSamlCreate('allowUnsolicitedResponse', event.target.checked)}
                  />
                  Allow Unsolicited
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={samlCreate.enabled}
                    onChange={(event) => updateSamlCreate('enabled', event.target.checked)}
                  />
                  Enabled
                </label>
                <button type="submit">Upsert</button>
              </div>
              <textarea
                className="json-editor"
                rows={4}
                placeholder="Signing Certificate PEM (optional)"
                value={samlCreate.signingCertificatePem}
                onChange={(event) => updateSamlCreate('signingCertificatePem', event.target.value)}
              />
            </form>

            <div className="detail-layout">
              <section className="list-panel">
                <h3>SAML SP List</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Name</th>
                      <th>Binding</th>
                      <th>Enabled</th>
                    </tr>
                  </thead>
                  <tbody>
                    {samlProviders.length === 0 && (
                      <tr>
                        <td colSpan={4} className="empty-cell">
                          No SAML service providers found.
                        </td>
                      </tr>
                    )}
                    {samlProviders.map((provider) => (
                      <tr
                        key={provider.id}
                        className={`clickable-row ${selectedSamlCode === provider.code ? 'active-row' : ''}`}
                        onClick={() => setSelectedSamlCode(provider.code)}
                      >
                        <td>{provider.code}</td>
                        <td>{provider.name}</td>
                        <td>{provider.bindingType === 2 ? 'HTTP-Redirect' : 'HTTP-POST'}</td>
                        <td>{provider.enabled ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="detail-panel">
                <h3>SAML SP Detail</h3>
                {!samlEdit && <p className="empty-hint">Select a SAML service provider to edit.</p>}
                {samlEdit && (
                  <form onSubmit={handleSaveSamlProvider} className="config-form">
                    <div className="inline-form">
                      <input value={samlEdit.code} disabled className="readonly-input" />
                      <input
                        placeholder="Name"
                        value={samlEdit.name}
                        onChange={(event) => updateSamlEdit('name', event.target.value)}
                        required
                      />
                      <input
                        placeholder="Entity ID"
                        value={samlEdit.entityId}
                        onChange={(event) => updateSamlEdit('entityId', event.target.value)}
                        required
                      />
                    </div>
                    <div className="inline-form">
                      <input
                        placeholder="ACS URL"
                        value={samlEdit.assertionConsumerServiceUrl}
                        onChange={(event) => updateSamlEdit('assertionConsumerServiceUrl', event.target.value)}
                        required
                      />
                      <input
                        placeholder="SLO URL"
                        value={samlEdit.singleLogoutServiceUrl}
                        onChange={(event) => updateSamlEdit('singleLogoutServiceUrl', event.target.value)}
                      />
                    </div>
                    <div className="inline-form">
                      <input
                        placeholder="NameIdFormat"
                        value={samlEdit.nameIdFormat}
                        onChange={(event) => updateSamlEdit('nameIdFormat', event.target.value)}
                      />
                      <input
                        placeholder="Audience"
                        value={samlEdit.audience}
                        onChange={(event) => updateSamlEdit('audience', event.target.value)}
                      />
                      <input
                        placeholder="RelayState default"
                        value={samlEdit.relayStateDefault}
                        onChange={(event) => updateSamlEdit('relayStateDefault', event.target.value)}
                      />
                    </div>
                    <div className="inline-form">
                      <select
                        value={samlEdit.bindingType}
                        onChange={(event) => updateSamlEdit('bindingType', event.target.value as SamlBindingTypeValue)}
                      >
                        <option value="1">HTTP-POST</option>
                        <option value="2">HTTP-Redirect</option>
                      </select>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={samlEdit.wantSignedAssertions}
                          onChange={(event) => updateSamlEdit('wantSignedAssertions', event.target.checked)}
                        />
                        Want Signed Assertions
                      </label>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={samlEdit.allowUnsolicitedResponse}
                          onChange={(event) => updateSamlEdit('allowUnsolicitedResponse', event.target.checked)}
                        />
                        Allow Unsolicited
                      </label>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={samlEdit.enabled}
                          onChange={(event) => updateSamlEdit('enabled', event.target.checked)}
                        />
                        Enabled
                      </label>
                    </div>
                    <textarea
                      className="json-editor"
                      rows={4}
                      placeholder="Signing Certificate PEM"
                      value={samlEdit.signingCertificatePem}
                      onChange={(event) => updateSamlEdit('signingCertificatePem', event.target.value)}
                    />
                    <div className="action-row">
                      <button type="submit">Save Changes</button>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => {
                          const target = samlProviders.find((provider) => provider.code === selectedSamlCode)
                          if (target) {
                            setSamlEdit(samlDraftFromEntity(target))
                          }
                        }}
                      >
                        Reset
                      </button>
                      <button type="button" className="danger-btn" onClick={() => void handleDeleteSamlProvider()}>
                        Delete
                      </button>
                    </div>
                  </form>
                )}
              </section>
            </div>
          </div>
        )}

        {activeTab === 'scim' && (
          <div className="panel">
            <h2>SCIM Tokens</h2>
            <p className="section-hint">Create and revoke SCIM access tokens.</p>
            <form onSubmit={handleCreateScimToken} className="config-form create-section">
              <h3>Create SCIM Token</h3>
              <div className="inline-form">
                <input
                  placeholder="Token Name"
                  value={scimCreate.name}
                  onChange={(event) => updateScimCreate('name', event.target.value)}
                  required
                />
                <input
                  type="number"
                  min="1"
                  placeholder="Expires In Days"
                  value={scimCreate.expiresInDays}
                  onChange={(event) => updateScimCreate('expiresInDays', event.target.value)}
                  required
                />
                <button type="submit">Create</button>
              </div>
            </form>

            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Active</th>
                  <th>Expires</th>
                  <th>Last Used</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {scimTokens.length === 0 && (
                  <tr>
                    <td colSpan={5} className="empty-cell">
                      No SCIM tokens found.
                    </td>
                  </tr>
                )}
                {scimTokens.map((token) => (
                  <tr key={token.id}>
                    <td>{token.name}</td>
                    <td>{token.isActive ? 'Yes' : 'No'}</td>
                    <td>{formatDateTime(token.expiresTime)}</td>
                    <td>{formatDateTime(token.lastUsedTime)}</td>
                    <td>
                      {token.isActive && (
                        <button className="danger-btn" onClick={() => void handleRevokeScimToken(token)}>
                          Revoke
                        </button>
                      )}
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
                {audits.length === 0 && (
                  <tr>
                    <td colSpan={4} className="empty-cell">
                      No audit events found.
                    </td>
                  </tr>
                )}
                {audits.map((audit) => (
                  <tr key={audit.id}>
                    <td>{formatDateTime(audit.occurredTime)}</td>
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
