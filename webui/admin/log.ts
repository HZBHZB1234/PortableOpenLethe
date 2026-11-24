import { reactive } from 'vue'

export interface LogEntry {
  id: number
  at: string
  path: string
  status: number
  ms: number
  preview: string
}

export const logs = reactive<LogEntry[]>([])
let seq = 0

export function pushLog(entry: Omit<LogEntry, 'id' | 'at'>) {
  logs.unshift({
    ...entry,
    id: ++seq,
    at: new Date().toLocaleTimeString('zh-CN', { hour12: false }),
  })
  if (logs.length > 200) logs.length = 200
}

export function clearLogs() {
  logs.splice(0)
}
