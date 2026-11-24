<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RefreshCw, Plus, Trash2, Pencil, Check, X } from 'lucide-vue-next'
import { call } from '../api'
import { toast } from '../../shared/toast'

interface Account {
  Id: string
  Username: string
  IngameId: number
  DiscordId: string | null
  CreatedAt: string
  UpdatedAt: string
}

const accounts = ref<Account[]>([])
const busy = ref(false)
const newName = ref('')
const newIngameId = ref('')
const editing = ref('')
const editId = ref('')

async function load() {
  busy.value = true
  const r = await call('/admin/accounts', {})
  if (r.ok) accounts.value = r.data?.accounts ?? []
  else toast('无法加载账号列表', 'error')
  busy.value = false
}

async function create() {
  const name = newName.value.trim()
  if (!name) { toast('请输入用户名', 'error'); return }
  busy.value = true
  // POST /admin/accounts/create  { token, username } -> { username, ingameId }
  const r = await call('/admin/accounts/create', { username: name })
  if (r.ok) {
    toast('账号已创建', 'success')
    const id = Number(newIngameId.value)
    if (newIngameId.value && Number.isFinite(id)) {
      // POST /admin/accounts/ingameid  { token, username, ingameId }
      const ir = await call('/admin/accounts/ingameid', { username: name, ingameId: id })
      if (!ir.ok) toast('Ingame ID 设置失败', 'error')
    }
    newName.value = ''
    newIngameId.value = ''
  } else toast(r.status === 409 ? '请求失败：冲突' : '创建失败', 'error')
  await load()
  busy.value = false
}

async function remove(a: Account) {
  if (!window.confirm(`删除账号 ${a.Username}?`)) return
  // POST /admin/accounts/delete  { token, username } -> { deleted: true }
  const r = await call('/admin/accounts/delete', { username: a.Username })
  if (r.ok) toast('账号已删除', 'success')
  else toast('删除失败', 'error')
  await load()
}

async function saveId(a: Account) {
  const id = Number(editId.value)
  if (!Number.isFinite(id)) { toast('请输入有效数字', 'error'); return }
  // POST /admin/accounts/ingameid  { token, username, ingameId } -> { ingameId }
  const r = await call('/admin/accounts/ingameid', { username: a.Username, ingameId: id })
  if (r.ok) { toast('Ingame ID 已更新', 'success'); editing.value = '' }
  else toast(r.status === 409 ? '该 Ingame ID 已被占用' : '更新失败', 'error')
  await load()
}

function shortDate(s?: string) { return s ? s.replace('T', ' ').slice(0, 19) : '—' }

onMounted(load)
</script>

<template>
  <div>
    <div class="panel">
      <div class="panel-head">
        <div><span class="eyebrow">Roster</span><h2>账号列表</h2><p>创建、删除本地私服账号</p></div>
        <button class="btn btn-outline" :disabled="busy" @click="load"><RefreshCw /> Refresh</button>
      </div>

      <form class="create-row" @submit.prevent="create">
        <label class="field"><span>新用户名 / Username</span>
          <input v-model="newName" placeholder="username" autocomplete="off"></label>
        <label class="field narrow"><span>Ingame ID (可选)</span>
          <input v-model="newIngameId" inputmode="numeric" placeholder="e.g. 1001"></label>
        <button class="btn btn-primary" type="submit" :disabled="busy"><Plus /> Create</button>
      </form>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Username</th><th>Ingame ID</th><th>Discord ID</th>
              <th>Created</th><th>Updated</th><th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!accounts.length"><td colspan="6" class="empty">暂无账号 · No accounts</td></tr>
            <tr v-for="a in accounts" :key="a.Id">
              <td><strong>{{ a.Username }}</strong></td>
              <td class="mono">
                <template v-if="editing === a.Username">
                  <input v-model="editId" class="inline-input" inputmode="numeric">
                </template>
                <template v-else>{{ a.IngameId }}</template>
              </td>
              <td class="muted mono">{{ a.DiscordId || '—' }}</td>
              <td class="muted mono">{{ shortDate(a.CreatedAt) }}</td>
              <td class="muted mono">{{ shortDate(a.UpdatedAt) }}</td>
              <td class="actions">
                <template v-if="editing === a.Username">
                  <button class="btn btn-outline" title="保存" @click="saveId(a)"><Check /></button>
                  <button class="btn btn-ghost" title="取消" @click="editing = ''"><X /></button>
                </template>
                <template v-else>
                  <button class="btn btn-ghost" title="编辑 Ingame ID"
                          @click="editing = a.Username; editId = String(a.IngameId)"><Pencil /></button>
                  <button class="btn btn-danger" title="删除" @click="remove(a)"><Trash2 /></button>
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="field-help">共 {{ accounts.length }} 个账号 · 数据来自 POST /admin/accounts</p>
    </div>
  </div>
</template>

<style scoped>
.create-row { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 12px; margin-bottom: 18px; }
.create-row .field { flex: 1; min-width: 220px; }
.create-row .field.narrow { max-width: 190px; }
.inline-input { width: 90px; padding: 4px 7px; font-family: var(--font-mono); font-size: 12px; }
.actions { white-space: nowrap; text-align: right; }
.actions .btn { width: 28px; height: 28px; padding: 0; margin-left: 4px; }
</style>
