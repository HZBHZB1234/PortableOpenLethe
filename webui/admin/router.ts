import { createRouter, createWebHashHistory } from 'vue-router'
import OverviewView from './views/OverviewView.vue'
import AccountsView from './views/AccountsView.vue'
import DocumentsView from './views/DocumentsView.vue'
import ActivityView from './views/ActivityView.vue'

// Hash mode: the static host never needs an SPA fallback route.
const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/overview' },
    {
      path: '/overview', component: OverviewView,
      meta: { title: '概览', eyebrow: 'Overview', desc: '本地服务运行状态与快捷操作' },
    },
    {
      path: '/accounts', component: AccountsView,
      meta: { title: '账号管理', eyebrow: 'Accounts', desc: '创建、删除与管理本地私服账号' },
    },
    {
      path: '/documents', component: DocumentsView,
      meta: { title: '文档编辑', eyebrow: 'Documents', desc: '直接修改账号的 JSON 文档字段' },
    },
    {
      path: '/activity', component: ActivityView,
      meta: { title: '请求日志', eyebrow: 'Activity', desc: '管理 API 的最近响应与运行状态' },
    },
    { path: '/:pathMatch(.*)*', redirect: '/overview' },
  ],
})

export default router
