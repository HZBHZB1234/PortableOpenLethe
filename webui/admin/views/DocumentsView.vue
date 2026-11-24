<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Save, FileJson, RefreshCw, TriangleAlert } from 'lucide-vue-next'
import { call, DOCUMENT_FIELDS } from '../api'
import { toast } from '../../shared/toast'

interface Account { Username: string; IngameId: number }

const accounts = ref<Account[]>([])
const username = ref('')
const field = ref<string>('UserInfo')
const value = ref('{}')
const busy = ref(false)
const schemaCount = ref<number | null>(null)

const filtered = computed(() =>
  username.value
    ? accounts.value.filter(a => a.Username.toLowerCase().includes(username.value.toLowerCase())).slice(0, 8)
    : accounts.value.slice(0, 8),
)

async function loadAccounts() {
  const r = await call('/admin/accounts', {})
  if (r.ok) accounts.value = r.data?.accounts ?? []
  else toast('无法加载账号列表', 'error')
}

async function save() {
  if (!username.value) { toast('请输入用户名', 'error'); return }
  try { JSON.parse(value.value) }
  catch { toast('JSON 格式无效', 'error'); return }
  busy.value = true
  // POST /admin/accounts/document  { token, username, field, value } -> { ok: true }
  const r = await call('/admin/accounts/document', {
    username: username.value, field: field.value, value: value.value,
  })
  if (r.ok) toast(`已保存 ${field.value} · ${username.value}`, 'success')
  else toast(r.status === 400 ? '字段或 JSON 无效' : '保存失败', 'error')
  busy.value = false
}

watch(username, () => { /* free text + datalist; no side effects needed */ })

onMounted(async () => {
  await loadAccounts()
  // POST /admin/config/schema — plain JSON text, token still carried in body.
  const r = await call('/admin/config/schema', {})
  if (r.ok) schemaCount.value = Array.isArray(r.data?.keys) ? r.data.keys.length : null
})
</script>

<template>
  <div>
    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Document editor</span><h2>编辑账号文档</h2><p>直接写入账号的 JSON 文档字段</p></div>
        <button class="btn btn-outline" @click="loadAccounts"><RefreshCw /> Refresh</button>
      </div>

      <div class="editor-row">
        <label class="field"><span>用户名 / Username</span>
          <input v-model="username" list="doc-users" placeholder="username" autocomplete="off">
          <datalist id="doc-users">
            <option v-for="a in filtered" :key="a.Username" :value="a.Username">
              {{ a.IngameId }}
            </option>
          </datalist>
        </label>
        <label class="field narrow"><span>字段 / Field</span>
          <select v-model="field">
            <option v-for="f in DOCUMENT_FIELDS" :key="f" :value="f">{{ f }}</option>
          </select>
        </label>
      </div>

      <label class="field">
        <span>JSON 内容 / Value</span>
        <textarea v-model="value" spellcheck="false" rows="16" placeholder="{}"></textarea>
      </label>

      <div class="editor-foot">
        <button class="btn btn-primary" :disabled="busy" @click="save"><Save /> {{ busy ? 'Saving…' : 'Save document' }}</button>
        <span class="field-help">
          POST /admin/accounts/document · 服务端仅提供写入，未暴露读取端点
        </span>
      </div>
    </div>

    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Reference</span><h2>字段说明</h2><p>字段白名单与配置模式</p></div>
        <FileJson class="ref-glyph" />
      </div>
      <div class="ref-grid">
        <div>
          <div class="status-key"><TriangleAlert /> 白名单字段（{{ DOCUMENT_FIELDS.length }}）</div>
          <div class="chips">
            <span v-for="f in DOCUMENT_FIELDS" :key="f" class="tag mono" :class="{ on: field === f }"
                  @click="field = f">{{ f }}</span>
          </div>
        </div>
        <div class="ref-side">
          <span class="eyebrow">Config schema</span>
          <p class="muted">{{ schemaCount === null ? '不可用' : `${schemaCount} 个可配置项` }}</p>
          <span class="field-help">POST /admin/config/schema</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.editor-row { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 18px; }
.editor-row .field { flex: 1; min-width: 220px; }
.editor-row .field.narrow { max-width: 240px; }
textarea { min-height: 300px; }
.editor-foot { display: flex; align-items: center; gap: 16px; margin-top: 14px; flex-wrap: wrap; }
.ref-glyph { width: 20px; height: 20px; color: var(--faint); }
.ref-grid { display: grid; grid-template-columns: minmax(0, 1fr) 180px; gap: 26px; }
.status-key { display: flex; align-items: center; gap: 8px; color: var(--muted); font-size: 11px; margin-bottom: 12px; }
.status-key svg { width: 13px; height: 13px; color: var(--accent-bright); }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chips .tag { cursor: pointer; font-size: 10px; }
.chips .tag:hover, .chips .tag.on { border-color: var(--accent-line); color: var(--accent-bright); }
.ref-side p { font-size: 12px; margin: 6px 0 4px; }
@media (max-width: 900px) { .ref-grid { grid-template-columns: 1fr; } }
</style>
