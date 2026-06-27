# eIAM to NetIAM Mapping

## Module-Level Mapping

| eiam Module | NetIAM Module | Notes |
|---|---|---|
| `eiam-authentication-*` | `NetIAM.Integrations` + `NetIAM.PortalApi` | provider adapters and callback orchestration |
| `eiam-identity-source-*` | `NetIAM.Integrations` + `NetIAM.Infrastructure.Services.DirectorySyncService` | pull + webhook normalization |
| `eiam-synchronizer` | `NetIAM.SyncWorker` + `NetIAM.AdminApi` webhook endpoint | background polling + event intake |
| `eiam-protocol-oidc/jwt` | `NetIAM.AuthServer` + OpenIddict | OIDC/JWT service capability |
| `eiam-audit` | `NetIAM.Infrastructure.Services.AuditService` | audit entry persistence to `eiam_audit` |

## Data Model Mapping

| eiam Table | NetIAM Entity / Table |
|---|---|
| `eiam_user` | `NetIamIdentityUser` mapped to `eiam_user` |
| `eiam_organization` | `OrganizationEntity` |
| `eiam_organization_member` | `OrganizationMemberEntity` |
| `eiam_identity_provider` | `IdentityProviderEntity` |
| `eiam_identity_source` | `IdentitySourceEntity` |
| `eiam_third_party_user` | `ThirdPartyUserEntity` |
| `eiam_user_idp_bind` | `UserIdpBindEntity` |
| `eiam_app` | `AppEntity` |
| `eiam_app_access_policy` | `AppAccessPolicyEntity` |
| `eiam_identity_source_sync_history` | `IdentitySourceSyncHistoryEntity` |
| `eiam_identity_source_sync_record` | `IdentitySourceSyncRecordEntity` |
| `eiam_audit` | `AuditEventEntity` |

## External SSO Mapping

- DingTalk
  - authorize URL: `login.dingtalk.com/oauth2/auth`
  - callback code support: `authCode`/`code`
- Feishu
  - OAuth2 code flow via `passport.feishu.cn`
  - callback and user profile mapping to `ExternalUserProfile`
- WeCom
  - `corpId + appSecret` token fetch
  - `code -> userId/openId` mapping with short-lived synthesized token

## Binding Strategy

1. find existing `ThirdPartyUser` (`tenant + provider + openId`)
2. if bound in `UserIdpBind`, login directly
3. if not bound, auto-bind by `NetIamIdentityUser.ExternalId == openId`
4. if still unresolved, return pending state and require explicit bind API

## Current Scope

- implemented: local identity, provider adapters, account binding, pull/webhook sync foundation, OIDC server setup
- planned hardening: full third-party API coverage, richer role/permission model, SCIM/SAML extensions
