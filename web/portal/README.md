# NetIAM Portal

Self-service portal for NetIAM users. The current implementation covers the first migration slice from eiam Portal:

- assigned applications
- profile update
- password change
- identity provider bindings and unbind
- personal audit timeline
- personal sessions and revocation
- DingTalk, Feishu, and WeCom SSO callback debugging

The portal still uses the shared debug request context (`X-Tenant-Id`, `X-Acting-User-Id`, `X-Session-Id`, and optional bearer token). A later migration step should replace this with first-class login/session state, route guards, OTP login, and forgot-password flows.

## Scripts

- `npm run dev` starts the Vite dev server.
- `npm run build` type-checks and builds the portal.
- `npm run lint` runs oxlint.

## Environment

- `VITE_PORTAL_API_BASE` defaults to `https://localhost:7003`.
- `VITE_TENANT_ID` defaults to `tenant-default`.
- `VITE_ACTING_USER_ID` defaults to empty.

The same values can be edited in the portal context strip at runtime and are persisted to local storage.
