import { createRouter, createWebHashHistory } from 'vue-router'
import LoginView from './views/LoginView.vue'
import DoneView from './views/DoneView.vue'

const router = createRouter({
  history: createWebHashHistory('/auth/'),
  routes: [
    { path: '/', redirect: '/login' },
    { path: '/login', component: LoginView },
    { path: '/done', component: DoneView },
    { path: '/:pathMatch(.*)*', redirect: '/login' },
  ],
})

export default router
