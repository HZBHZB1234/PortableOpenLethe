<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { CircleHelp } from 'lucide-vue-next'
import Wordmark from '../../shared/Wordmark.vue'

const route = useRoute()
const name = computed(() => {
  const q = route.query.name
  return (Array.isArray(q) ? q[0] : q) || 'player'
})
const closed = ref(false)

function backToLauncher() {
  window.close()
  // Browsers refuse window.close() for tabs they did not open.
  window.setTimeout(() => { closed.value = true }, 250)
}
</script>

<template>
  <div class="auth-view">
    <div class="auth-card done-card">
      <Wordmark sub="Local login" />

      <div class="done-mark"><span class="diamond filled"></span></div>
      <div class="card-heading center">
        <span class="eyebrow accent">Session established</span>
        <h1 class="serif">登录成功</h1>
        <p class="muted">凭据已交付 Launcher，你可以关闭此窗口返回游戏。</p>
      </div>

      <div class="user-pill mono">{{ name }}</div>

      <button class="btn btn-primary submit-btn" @click="backToLauncher">返回 Launcher</button>
      <p v-if="closed" class="field-help center-text">
        浏览器阻止了自动关闭，请手动关闭此标签页。
      </p>

      <div class="handoff">
        <span class="diamond ok"></span>
        <span>凭据已缓存 · 会话将在 60 秒后失效</span>
      </div>
    </div>

    <section class="faq-block">
      <div class="section-eyebrow"><CircleHelp /> <span class="eyebrow">Frequently asked</span></div>
      <div class="faq">
        <details open>
          <summary>接下来做什么？</summary>
          <p>关闭本页面并返回 Launcher。Launcher 会自动轮询并完成后续登录流程。</p>
        </details>
        <details>
          <summary>可以更换账号吗？</summary>
          <p>从 Launcher 重新发起登录即可再次选择用户名。</p>
        </details>
      </div>
    </section>
  </div>
</template>
