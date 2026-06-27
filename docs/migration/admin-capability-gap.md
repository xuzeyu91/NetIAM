# NetIAM Admin Capability Gap Matrix (vs eiam CE Code Baseline)

## Scope and Baseline

- Baseline source: `D:/AI/参考项目/eiam` **code implementation** (not README product vision).
- Focus scope: Admin console closure for tenant/user/organization/app/rbac/saml/scim/audit domains.
- Current NetIAM status snapshot is based on:
  - `src/NetIAM.AdminApi/Controllers/*`
  - `web/admin/src/App.tsx`
  - `docs/migration/final-gap-check.md`

## Capability Matrix

| Capability Domain | eiam CE (code) | NetIAM Backend | NetIAM Admin UI | Gap Type | Current Priority |
| --- | --- | --- | --- | --- | --- |
| Tenants | list/create/update/delete | list/create | none | UI missing + API partial | P0 |
| Users | full CRUD and ops | full CRUD | list only | UI missing | P0 |
| Organizations | full CRUD/tree | list/create | none | UI missing + API partial | P0 |
| Apps | full CRUD and config | list/create | none | UI missing + API partial | P0 |
| RBAC Permissions | list/create | list/create | none | UI missing | P0 |
| RBAC Roles | list/create/delete + assignment | list + assignment only | none | UI missing + API partial | P0 |
| User Permission Grants | query/effective/grant | query/effective/grant | none | UI missing | P0 |
| Identity Providers | CRUD | CRUD | CRUD | mostly aligned | P1 |
| Identity Sources | CRUD + sync/history | CRUD + sync | CRUD + sync | history not exposed | P1 |
| SAML SP Config | CRUD | upsert/list/delete | none | UI missing | P0 |
| SCIM Token Management | list/create/revoke | list/create/revoke | none | UI missing | P0 |
| Audit Query | list/filter | list (take) | list | mostly aligned | P1 |

## Quantitative Snapshot (Before This Iteration)

- Admin domain coverage count (this scope): `12` capability lines.
- Backend available or partial: `11/12` (91.7%) in this focused scope.
- Admin UI available (operable): `4/12` (33.3%).
- Closure bottleneck: UI and partial CRUD APIs, not protocol core.

## Execution Goal for This Iteration

1. Raise Admin UI operable coverage to `>= 10/12`.
2. Remove partial CRUD gaps in:
   - tenant
   - organization
   - app
   - role lifecycle
3. Keep non-admin and protocol expansions out of scope (CAS/LDAP/MFA etc.).

## Out of Scope in This Matrix

- eiam CE security/setting/monitor modules not yet mapped to NetIAM Admin.
- README-only capabilities not present in eiam CE code (CAS/SAML2/SP, SCIM bulk, LDAP/AD source).
- Portal UX and auth session productization.
