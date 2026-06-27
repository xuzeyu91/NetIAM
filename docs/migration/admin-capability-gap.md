# NetIAM Admin Capability Gap Matrix (vs eiam CE Code Baseline)

## Scope and Baseline

- Baseline source: `D:/AI/参考项目/eiam` **code implementation** (not README product vision).
- Focus scope: Admin console closure for tenant/user/user-group/organization/app/access-policy/rbac/saml/scim/audit domains.
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
| RBAC Permissions | list/create | list/create | list/create | partial (missing bulk ops) | P2 |
| RBAC Roles | list/create/delete + assignment | list/create/delete + assignment | list/create/delete + assignment | partial (missing role-user UX) | P2 |
| User Permission Grants | query/effective/grant | query/effective/grant | query/effective/grant | partial (missing revoke UX) | P2 |
| Identity Providers | CRUD | CRUD | CRUD | mostly aligned | P1 |
| Identity Sources | CRUD + sync/history | CRUD + sync | CRUD + sync | history not exposed | P1 |
| SAML SP Config | CRUD | upsert/list/delete | upsert/list/delete | mostly aligned | P1 |
| SCIM Token Management | list/create/revoke | list/create/revoke | list/create/revoke | mostly aligned | P1 |
| Audit Query | list/filter | list (take) | list | mostly aligned | P1 |

## Quantitative Snapshot (After Second-Phase Closure)

- Admin domain coverage count (this scope): `14` capability lines.
- Backend available or partial: `14/14` (100%) in this focused scope.
- Admin UI available (operable): `14/14` (100%) in this focused scope.
- Remaining gap shifts from "UI/API closure" to "depth and advanced modules".

## Execution Goal for This Iteration

1. Keep current admin closure stable and regression-free.
2. Extend into eiam modules outside current scope:
   - security settings
   - monitor/session
   - system settings (message/geoip/storage)
3. Fill depth gaps:
   - identity source sync history query and visualization
   - richer RBAC user-role and grant revoke UX.

## Out of Scope in This Matrix

- eiam CE security/setting/monitor modules not yet mapped to NetIAM Admin.
- README-only capabilities not present in eiam CE code (CAS/SAML2/SP, SCIM bulk, LDAP/AD source).
- Portal UX and auth session productization.
