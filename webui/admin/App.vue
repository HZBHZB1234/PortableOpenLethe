<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import {
  LayoutDashboard, Users, FileJson, ScrollText, LogOut,
  ShieldCheck, Server, ChevronRight, LockKeyhole,
} from 'lucide-vue-next'
import Wordmark from '../shared/Wordmark.vue'
import ToastHost from '../shared/ToastHost.vue'
import { toast } from '../shared/toast'
import { clearToken, getToken, loginAdmin, setToken } from './api'

const route = useRoute()
const token = ref(getToken())
const username = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')

const nav = [
  { to: '/overview', label: '概览', icon: LayoutDashboard },
  { to: '/accounts', label: '账号管理', icon: Users },
  { to: '/documents', label: '文档编辑', icon: FileJson },
  { to: '/activity', label: '请求日志', icon: ScrollText },
]

const meta = computed(() => route.meta as { title?: string; eyebrow?: string; desc?: string })
const eyebrow = computed(() => meta.value.eyebrow ?? 'Overview')
const title = computed(() => meta.value.title ?? '概览')
const desc = computed(() => meta.value.desc ?? '')

const unauthorized = () => {
  token.value = ''
  toast('会话已过期，请重新登录', 'error')
}
onMounted(() => window.addEventListener('pl-admin-unauthorized', unauthorized))
onUnmounted(() => window.removeEventListener('pl-admin-unauthorized', unauthorized))

async function signIn() {
  if (!password.value) { error.value = '请输入管理密码'; return }
  error.value = ''
  busy.value = true
  try {
    const t = await loginAdmin(username.value, password.value)
    setToken(t)
    token.value = t
    password.value = ''
    toast('已登录 · ADMIN', 'success')
  } catch (e) {
    error.value = e instanceof Error ? e.message : '登录失败'
  } finally {
    busy.value = false
  }
}

function signOut() {
  clearToken()
  token.value = ''
  password.value = ''
}
</script>

<template>
  <ToastHost />

  <!-- ── Auth gate ─────────────────────────────────────────────────────── -->
  <div v-if="!token" class="gate">
    <div class="gate-inner">
      <Wordmark sub="Administration" />
      <div class="gate-card">
        <span class="eyebrow accent">Restricted access</span>
        <h1 class="serif">管理控制台</h1>
        <p class="muted">输入管理密码以查看账号、文档与运行状态。</p>
        <hr class="hairline">

        <form @submit.prevent="signIn">
          <label class="field">
            <span>用户名 / Username</span>
            <input v-model="username" autocomplete="username" placeholder="admin (optional)">
          </label>
          <label class="field">
            <span>管理密码 / Password</span>
            <input v-model="password" type="password" autocomplete="current-password"
                   placeholder="admin password" autofocus>
          </label>
          <button class="btn btn-primary gate-submit" type="submit" :disabled="busy">
            <LockKeyhole /> {{ busy ? 'Authenticating…' : 'Sign in' }}
          </button>
        </form>
        <p v-if="error" class="gate-error">{{ error }}</p>

        <div class="gate-foot">
          <span class="diamond"></span>
          本地服务端 · 会话仅保存在此标签页
        </div>
      </div>
      <div class="site-footer">
        <span>© LETHE · Portable private server</span>
        <span>Not an official Project Moon service</span>
      </div>
    </div>
  </div>

  <!-- ── Console shell ─────────────────────────────────────────────────── -->
  <div v-else class="shell">
    <header class="topbar">
      <Wordmark sub="Operations console" compact />
      <span class="status-pill"><span class="diamond ok"></span> LOCAL SERVER</span>
      <div class="spacer"></div>
      <span class="session-label"><ShieldCheck /> ADMIN SESSION</span>
      <button class="btn btn-ghost" @click="signOut"><LogOut /> Sign out</button>
    </header>

    <div class="layout">
      <aside class="sidebar">
        <div class="nav-label">Workspace</div>
        <RouterLink v-for="item in nav" :key="item.to" :to="item.to" class="nav-item">
          <component :is="item.icon" />
          <span>{{ item.label }}</span>
          <ChevronRight class="nav-chevron" />
        </RouterLink>
        <div class="sidebar-bottom">
          <div class="nav-label">Instance</div>
          <div class="status-row"><span class="diamond ok"></span> API · online</div>
          <div class="status-row"><span class="diamond"></span> Auth · local</div>
          <div class="status-row"><span class="diamond warn"></span> Data · SQLite</div>
        </div>
      </aside>

      <main class="content">
        <div class="page-head">
          <div>
            <span class="eyebrow accent">{{ eyebrow }}</span>
            <h1 class="serif">{{ title }}</h1>
            <p class="muted">{{ desc }}</p>
          </div>
          <Server class="page-glyph" />
        </div>
        <RouterView />
      </main>
    </div>
  </div>
</template>
