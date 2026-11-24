import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import '../shared/theme.css'
import './auth.css'

// AuthEndpoints serves this SPA at BOTH /auth/ and /auth/login?session_id=...
// (the launcher opens the latter). Capture the handoff id from the query string
// before normalising the address to the SPA's canonical /auth/#/login route.
const params = new URLSearchParams(window.location.search)
const sid = params.get('session_id')
if (sid) sessionStorage.setItem('pl_auth_session_id', sid)
if (!window.location.hash || window.location.hash === '#') {
  window.history.replaceState(null, '', '/auth/#/login')
}

createApp(App).use(router).mount('#app')
