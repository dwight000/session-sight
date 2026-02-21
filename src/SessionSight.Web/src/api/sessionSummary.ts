import { fetchApi } from './client'
import type { SessionSummary } from '../types'

export function getSessionSummary(sessionId: string, regenerate?: boolean): Promise<SessionSummary> {
  const qs = regenerate ? '?regenerate=true' : ''
  return fetchApi<SessionSummary>(`/api/summary/session/${encodeURIComponent(sessionId)}${qs}`)
}
