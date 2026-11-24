<script setup lang="ts">
import { toasts, dismiss } from './toast'
</script>

<template>
  <div class="toast-host" aria-live="polite">
    <TransitionGroup name="toast">
      <div v-for="t in toasts" :key="t.id" class="toast" :class="t.tone" @click="dismiss(t.id)">
        <span class="diamond" :class="t.tone === 'success' ? 'ok' : ''"></span>
        <span>{{ t.text }}</span>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-host {
  position: fixed; top: 62px; right: 22px; z-index: 50;
  display: flex; flex-direction: column; gap: 8px; pointer-events: none;
}
.toast {
  pointer-events: auto; cursor: pointer;
  min-width: 230px; max-width: 360px;
  display: flex; align-items: center; gap: 10px;
  padding: 11px 14px;
  background: var(--surface-2); border: 1px solid var(--border);
  border-left: 2px solid var(--accent); border-radius: var(--radius);
  box-shadow: 0 12px 30px rgba(0, 0, 0, .5);
  color: var(--text); font-size: 12px;
}
.toast.success { border-left-color: var(--ok); }
.toast.error { border-left-color: var(--err); }
.toast-enter-active, .toast-leave-active { transition: opacity .18s ease, transform .18s ease; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translateX(14px); }
</style>
