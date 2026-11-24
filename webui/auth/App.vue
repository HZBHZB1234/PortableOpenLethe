<script setup lang="ts">
import ToastHost from '../shared/ToastHost.vue'
</script>

<template>
  <ToastHost />
  <div class="auth-shell">
    <div class="auth-grain"></div>
    <main class="auth-main"><RouterView /></main>
    <footer class="site-footer auth-footer">
      <span>© LETHE · Portable private server</span>
      <span class="footer-links">
        <a href="https://lethelc.site" target="_blank" rel="noreferrer">官网</a>
        <a href="/health">状态</a>
        <span>Not an official Project Moon service</span>
      </span>
    </footer>
  </div>
</template>
