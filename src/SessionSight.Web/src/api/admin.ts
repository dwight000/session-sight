import { fetchApi } from './client'

export interface ReindexResponse {
  queued: number
}

export async function reindexSession(sessionId: string): Promise<ReindexResponse> {
  return fetchApi<ReindexResponse>(
    `/api/admin/reindex?sessionId=${encodeURIComponent(sessionId)}`,
    { method: 'POST' },
  )
}
