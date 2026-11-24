# Portable Lethe Web UI

Vue 3 + Vite rewrite of the admin console (`/admin/#/`) and launcher login
(`/auth/#/` and `/auth/login?session_id=...`). Both entries are static SPAs with
hash routing, so no server-side SPA fallback is needed.

```bash
npm install
npm run build     # emits ../src/OpenLethe.Server/wwwroot/{admin,auth}/index.html + shared hashed assets/
npm run typecheck # vue-tsc --noEmit
```

`vite.config.ts` uses `base: '/'`: the auth HTML is also served from
`/auth/login`, so asset URLs must be root-absolute. `theme.css` is imported by
both entries and hoisted into one shared hashed CSS/JS chunk.

## Admin endpoints actually served by `Handlers/AdminEndpoints.cs`

All except login require the admin token **in the JSON body** (`{ ..., token }`);
the SPA also sends `Authorization: Bearer` for completeness.

| Action | Method + path | Body | Response |
| --- | --- | --- | --- |
| Login | `POST /admin/login` | `{ username, password }` (only `password` is read) | `{ token }` |
| List accounts | `POST /admin/accounts` | `{ token }` | `{ accounts: [...] }` |
| Create | `POST /admin/accounts/create` | `{ token, username }` | `{ username, ingameId }` |
| Delete | `POST /admin/accounts/delete` | `{ token, username }` | `{ deleted: true }` |
| Set ingame id | `POST /admin/accounts/ingameid` | `{ token, username, ingameId }` | `{ ingameId }` |
| Write document | `POST /admin/accounts/document` | `{ token, username, field, value }` | `{ ok: true }` |
| Config schema | `POST /admin/config/schema` | `{ token }` | JSON text |
| Restart request | `POST /admin/restart-request` | `{ token }` | `{ ok: true }` |

`field` must be one of the 19 names in `DOCUMENT_FIELDS` (`admin/api.ts`), copied
verbatim from `AdminEndpoints.DocumentFields`.

## Auth handoff

`GET /auth/login?session_id=...` serves `wwwroot/auth/index.html`; `main.ts`
captures `session_id` and the form POSTs `application/x-www-form-urlencoded`
`session_id` + `username` to the unchanged server-side `/auth/login/submit`.
The server parks the JWT for `/auth/token/poll` and 302-redirects to
`/auth/#/done?name=...`; launcher polling is untouched.
