# NetIAM Admin Capability Gap Matrix (vs eiam CE Code Baseline)

## Scope and Baseline

- Baseline source: `D:/AI/参考项目/eiam` **code implementation** (not README product vision).
- Focus scope: Admin console closure for tenant/user/user-group/organization/app/access-policy/security/system-setting/monitor/rbac/saml/scim/audit domains.
- Current NetIAM status snapshot is based on:
  - `src/NetIAM.AdminApi/Controllers/*`
  - `web/admin/src/App.tsx`
  - `docs/migration/final-gap-check.md`

## Capability Matrix

| Capability Domain | eiam CE (code) | NetIAM Backend | NetIAM Admin UI | Gap Type | Current Priority |
| --- | --- | --- | --- | --- | --- |
| Tenants | list/create/update/delete | full CRUD | full CRUD | aligned in current scope | P1 |
| Users | full CRUD and ops | full CRUD | full CRUD | aligned in current scope | P1 |
| User Groups | CRUD + member management | CRUD + member replacement | CRUD + member replacement | aligned in current scope | P1 |
| Organizations | full CRUD/tree | full CRUD/tree | full CRUD/tree | aligned in current scope | P1 |
| Apps | full CRUD and config | full CRUD | full CRUD | aligned in current scope | P1 |
| App Access Policies | CRUD + subject assignment | full CRUD + subject validation | full CRUD + subject selection UI | aligned in current scope | P1 |
| Security Basic Settings | session/captcha/concurrency policy | full read/write API | full edit UI | aligned in current scope | P1 |
| Password Policy Settings | complexity/history/age policy | full read/write API | full edit UI | aligned in current scope | P1 |
| Defense Policy Settings | lockout/risk policy | full read/write API | full edit UI | aligned in current scope | P1 |
| Administrator Management | list/add/remove admin | full API | full UI | aligned in current scope | P1 |
| System Message Settings | mail/sms provider + template | full read/write API | full edit UI | aligned in current scope | P1 |
| System Storage Settings | object storage config | full read/write API | full edit UI | aligned in current scope | P1 |
| System GeoIP Settings | geo source config | full read/write API | full edit UI | aligned in current scope | P1 |
| Monitor Sessions | list/revoke/hard-logout | list + revoke + session termination API | list + revoke UI + session-id propagation | mostly aligned | P1 |
| RBAC Permissions | list/create | list/create | list/create | partial (missing bulk ops) | P2 |
| RBAC Roles | list/create/delete + assignment | list/create/delete + role-user binding API | list/create/delete + role-user binding UI | mostly aligned | P1 |
| User Permission Grants | query/effective/grant/revoke | query/effective/grant/revoke | query/effective/grant/revoke | mostly aligned | P1 |
| Identity Providers | CRUD | CRUD | CRUD | mostly aligned | P1 |
| Identity Sources | CRUD + sync/history | CRUD + sync + history/record query | CRUD + sync + history/record query | mostly aligned | P1 |
| SAML SP Config | CRUD + signed/encrypted assertion + cert rollover | upsert/list/delete + signed/encrypted response + auto cert rotation | upsert/list/delete | mostly aligned | P1 |
| SCIM Provisioning + Token Management | users/groups CRUD+PATCH+bulk + token list/create/revoke | users/groups CRUD+PATCH filter semantics + bulk + token management | token/full CRUD admin UI (bulk via API) | mostly aligned | P1 |
| Audit Query | list/filter | list (take) | list | mostly aligned | P1 |

## Quantitative Snapshot (After Fourth-Phase Depth Closure)

- Admin domain coverage count (this scope): `22` capability lines.
- Backend available or partial: `22/22` (100%) in this focused scope.
- Admin UI available (operable): `22/22` (100%) in this focused scope.
- Remaining gap shifts from "module closure" to "depth semantics and protocol hardening".

## Execution Goal for Next Iteration

1. Keep current admin closure stable and regression-free.
2. Fill remaining depth gaps:
   - RBAC bulk operations and permission policy templates
   - end-session protocol completeness for full OIDC/SAML logout interop
   - sandbox credential governance and CI gating for contract/e2e pipelines.

## Out of Scope in This Matrix

- README-only capabilities not present in eiam CE code (CAS/SAML2/SP, SCIM bulk, LDAP/AD source).
- Portal UX and auth session productization.
