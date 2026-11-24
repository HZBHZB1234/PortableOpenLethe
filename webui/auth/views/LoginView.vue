<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowRight, CircleHelp, MessageCircle, BookOpen, Radio } from 'lucide-vue-next'
import Wordmark from '../../shared/Wordmark.vue'

const route = useRoute()
const router = useRouter()

const sessionId = computed(() => {
  const q = route.query.session_id
  return (typeof q === 'string' ? q : '') || sessionStorage.getItem('pl_auth_session_id') || ''
})
const username = ref('')
const busy = ref(false)
const error = ref('')

async function submit() {
  const name = username.value.trim()
  if (!name) { error.value = '请输入用户名'; return }
  if (!sessionId.value) { error.value = '会话已失效，请从 Launcher 重新打开登录页'; return }
  error.value = ''
  busy.value = true
  try {
    // Launcher handoff is unchanged: a real form POST to the server-side
    // endpoint, which parks the JWT in the session cache for /auth/token/poll.
    const form = new URLSearchParams()
    form.set('session_id', sessionId.value)
    form.set('username', name)
    const res = await fetch('/auth/login/submit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: form,
    })
    if (!res.ok) {
      error.value = res.status === 409 ? '账号创建冲突，请重试' : '登录失败，请检查会话或用户名'
      return
    }
    // The server redirects to /auth/#/done?name=... (or returns a themed page);
    // either way we drive the success state client-side with the safe, escaped name.
    router.push({ path: '/done', query: { name } })
  } catch (e) {
    error.value = `无法连接服务端: ${e instanceof Error ? e.message : 'unknown error'}`
  } finally {
    busy.value = false
  }
}

const community = [
  { icon: MessageCircle, title: 'COMMUNITY', text: '加入社区，获取安装与更新帮助。', href: 'https://lethelc.site' },
  { icon: BookOpen, title: 'GUIDE', text: '了解本地登录与 Launcher 的连接方式。', href: 'https://lethelc.site' },
  { icon: Radio, title: 'STATUS', text: '服务运行状态与健康检查。', href: '/health' },
]
</script>

<template>
  <div class="auth-view">
    <div class="auth-card">
      <Wordmark sub="Local login" />

      <div class="card-heading">
        <span class="eyebrow accent">Authentication</span>
        <h1 class="serif">登录私服</h1>
        <p class="muted">输入用户名以继续。首次登录将自动创建本地账号。</p>
      </div>

      <hr class="hairline">

      <form @submit.prevent="submit">
        <label class="field">
          <span>用户名 / Username</span>
          <input v-model="username" name="username" autocomplete="username"
                 placeholder="username" autofocus>
        </label>
        <button class="btn btn-primary submit-btn" type="submit" :disabled="busy">
          {{ busy ? 'Signing in…' : 'Sign in' }} <ArrowRight />
        </button>
      </form>
      <p v-if="error" class="auth-error">{{ error }}</p>

      <div class="handoff">
        <span class="diamond"></span>
        <span>会话 <span class="mono">{{ sessionId.slice(0, 10) || '—' }}…</span> · 由 Launcher 提供</span>
      </div>
    </div>

    <section class="faq-block">
      <div class="section-eyebrow"><CircleHelp /> <span class="eyebrow">Frequently asked</span></div>
      <div class="faq">
        <details open>
          <summary>为什么要登录？</summary>
          <p>本地服务端使用用户名标识你的存档与设置。首次登录会自动创建账号，无需注册。</p>
        </details>
        <details>
          <summary>我的数据存在哪里？</summary>
          <p>账号与存档保存在服务端本地 SQLite 数据库中，不会上传到任何外部服务。</p>
        </details>
        <details>
          <summary>登录完成后要做什么？</summary>
          <p>返回 Launcher 继续游戏即可。Launcher 会自动获取本次登录的凭据。</p>
        </details>
      </div>
    </section>

    <section class="community">
      <a v-for="c in community" :key="c.title" class="community-card" :href="c.href"
         :target="c.href.startsWith('http') ? '_blank' : undefined" rel="noreferrer">
        <component :is="c.icon" />
        <span class="eyebrow">{{ c.title }}</span>
        <span class="muted">{{ c.text }}</span>
      </a>
    </section>
  </div>
</template>
