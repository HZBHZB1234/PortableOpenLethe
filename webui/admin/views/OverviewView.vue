<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { RefreshCw, ArrowUpRight, Activity as ActivityIcon } from 'lucide-vue-next'
import { call } from '../api'
import { logs } from '../log'
import { toast } from '../../shared/toast'

const router = useRouter()
const accountCount = ref<number | null>(null)
const health = ref<'checking' | 'ok' | 'error'>('checking')
const busy = ref(false)

const lastStatus = computed(() => logs[0]?.status ?? '—')
const lastPath = computed(() => logs[0]?.path ?? '尚未发送请求')

async function refresh() {
  busy.value = true
  const r = await call('/admin/accounts', {})
  if (r.ok) accountCount.value = r.data?.accounts?.length ?? 0
  else toast('无法加载账号列表', 'error')
  busy.value = false
}

onMounted(async () => {
  await refresh()
  try {
    const res = await fetch('/health')
    health.value = res.ok && (await res.text()).trim() === 'ok' ? 'ok' : 'error'
  } catch { health.value = 'error' }
})

const instance = [
  ['服务 / Service', 'Portable Lethe · Local server'],
  ['运行时 / Runtime', '.NET 9 · framework-dependent'],
  ['存储 / Storage', 'SQLite · data/openlethe.db'],
  ['认证 / Auth', 'Local username · JWT handoff'],
  ['控制台 / Console', '/admin/#/  ·  session token'],
]
const quick = [
  { to: '/accounts', title: '账号管理', sub: '创建、删除与调整 Ingame ID', icon: RefreshCw },
  { to: '/documents', title: '文档编辑', sub: '写入账号的 JSON 文档字段', icon: ActivityIcon },
  { to: '/activity', title: '请求日志', sub: '查看最近管理 API 响应', icon: ArrowUpRight },
]
</script>

<template>
  <div>
    <div class="kpis">
      <div class="kpi">
        <span class="eyebrow">Accounts</span>
        <div class="value">{{ accountCount ?? '—' }}</div>
        <div class="hint">本地账号总数</div>
      </div>
      <div class="kpi">
        <span class="eyebrow">Latest request</span>
        <div class="value">{{ lastStatus }}</div>
        <div class="hint mono">{{ lastPath }}</div>
      </div>
      <div class="kpi">
        <span class="eyebrow">Session</span>
        <div class="value">Active</div>
        <div class="hint">管理员令牌已加载</div>
      </div>
    </div>

    <div class="panel">
      <div class="panel-head">
        <div>
          <span class="eyebrow">Status</span>
          <h2>运行状态</h2>
          <p>服务健康与实例信息</p>
        </div>
        <span class="tag" :class="health === 'ok' ? 'ok' : health === 'error' ? 'err' : ''">
          {{ health === 'ok' ? '● HEALTHY' : health === 'error' ? '● OFFLINE' : '… CHECKING' }}
        </span>
      </div>
      <div class="status-grid">
        <div v-for="[label, value] in instance" :key="label" class="status-item">
          <div class="status-key"><span class="diamond"></span>{{ label }}</div>
          <div class="status-value mono">{{ value }}</div>
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Shortcuts</span><h2>快速开始</h2><p>常用操作集中在这里</p></div>
        <button class="btn btn-outline" :disabled="busy" @click="refresh"><RefreshCw /> 刷新</button>
      </div>
      <div class="quick-grid">
        <button v-for="q in quick" :key="q.to" class="quick-link" @click="router.push(q.to)">
          <span class="diamond"></span>
          <span class="ql-text"><strong>{{ q.title }}</strong><small>{{ q.sub }}</small></span>
          <component :is="q.icon" />
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.status-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 28px; }
.status-item {
  display: flex; align-items: center; justify-content: space-between; gap: 14px;
  padding: 11px 0; border-bottom: 1px solid var(--border);
}
.status-key { display: flex; align-items: center; gap: 10px; color: var(--muted); font-size: 11px; white-space: nowrap; }
.status-value { color: var(--text); font-size: 11px; text-align: right; }
.quick-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
@media (max-width: 980px) { .status-grid, .quick-grid { grid-template-columns: 1fr; } }
</style>
