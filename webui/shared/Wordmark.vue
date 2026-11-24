<script setup lang="ts">
withDefaults(defineProps<{ sub?: string; compact?: boolean }>(), { sub: 'Portable server', compact: false })
</script>

<template>
  <div class="wordmark" :class="{ compact }">
    <span class="mark-text">LETHE</span>
    <span class="mark-sep"></span>
    <span v-if="sub" class="mark-sub">{{ sub }}</span>
  </div>
</template>
