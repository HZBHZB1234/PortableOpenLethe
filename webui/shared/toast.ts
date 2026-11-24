import { reactive } from 'vue'

export interface Toast {
  id: number
  text: string
  tone: 'info' | 'success' | 'error'
}

export const toasts = reactive<Toast[]>([])
let seq = 0

export function toast(text: string, tone: Toast['tone'] = 'info') {
  const id = ++seq
  toasts.push({ id, text, tone })
  window.setTimeout(() => dismiss(id), 4200)
}

export function dismiss(id: number) {
  const i = toasts.findIndex(t => t.id === id)
  if (i >= 0) toasts.splice(i, 1)
}
