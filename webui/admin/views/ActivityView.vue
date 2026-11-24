<script setup lang="ts">
import { computed } from 'vue'
import { Trash2, Activity } from 'lucide-vue-next'
import { logs, clearLogs } from '../log'

const okCount = computed(() => logs.filter(l => l.status < 400).length)
const errCount = computed(() => logs.filter(l => l.status >= 400).length)
const avgMs = computed(() => logs.length
  ? Math.round(logs.reduce((n, l) => n + l.ms, 0) / logs.length)
  : 0)

const endpoints = [
  ['POST', '/admin/login', '管理密码 → token'],
  ['POST', '/admin/accounts', '列出账号'],
  ['POST', '/admin/accounts/create', '创建账号'],
  ['POST', '/admin/accounts/delete', '删除账号'],
  ['POST', '/admin/accounts/ingameid', '修改 Ingame ID'],
  ['POST', '/admin/accounts/document', '写入文档字段'],
  ['POST', '/admin/config/schema', '配置模式'],
  ['POST', '/admin/restart-request', '请求重启'],
]
</script>

<template>
  <div>
    <div class="kpis">
      <div class="kpi"><span class="eyebrow">Requests</span><div class="value">{{ logs.length }}</div><div class="hint">本次会话记录数</div></div>
      <div class="kpi"><span class="eyebrow">Succeeded</span><div class="value">{{ okCount }}</div><div class="hint">HTTP &lt; 400</div></div>
      <div class="kpi"><span class="eyebrow">Failed</span><div class="value">{{ errCount }}</div><div class="hint">HTTP ≥ 400 · 平均 {{ avgMs }}ms</div></div>
    </div>

    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Client log</span><h2>请求日志</h2><p>最近的管理 API 响应（最多保留 200 条）</p></div>
        <button class="btn btn-outline" :disabled="!logs.length" @click="clearLogs"><Trash2 /> Clear</button>
      </div>
      <div class="log-view">
        <div v-if="!logs.length" class="log-empty">等待请求… 请求将在此实时显示。</div>
        <div v-for="l in logs" :key="l.id" class="log-row" :class="l.status >= 400 ? 'err' : 'ok'">
          <span class="at">{{ l.at }}</span>
          <span class="code">{{ l.status }}</span>
          <span class="ms">{{ l.ms }}ms</span>
          <span class="detail"><b>{{ l.path }}</b> <span class="preview">{{ l.preview }}</span></span>
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Reference</span><h2>API 端点</h2><p>控制台使用的现有管理端点</p></div>
        <Activity class="ref-glyph" />
      </div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Method</th><th>Path</th><th>用途</th></tr></thead>
          <tbody>
            <tr v-for="[m, p, d] in endpoints" :key="p">
              <td><span class="tag ok">{{ m }}</span></td>
              <td class="mono">{{ p }}</td>
              <td class="muted">{{ d }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="field-help">所有端点均使用 body-token 认证（token 在 JSON 请求体中）。</p>
    </div>
  </div>
</template>

<style scoped>
.log-empty { color: #55555a; padding: 18px 0; }
.log-row .at, .log-row .ms { color: #6d6c69; }
.log-row .detail b { color: #c7c5bf; font-weight: 500; }
.log-row .preview { color: #55555a; margin-left: 8px; }
.ref-glyph { width: 20px; height: 20px; color: var(--faint); }
.field-help { margin-top: 10px; }
</style>
