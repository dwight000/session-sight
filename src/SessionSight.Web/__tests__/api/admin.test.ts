import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { reindexSession } from '../../src/api/admin'

describe('admin api', () => {
  describe('reindexSession', () => {
    it('sends POST with sessionId query param', async () => {
      let capturedUrl = ''
      server.use(
        http.post('/api/admin/reindex', ({ request }) => {
          capturedUrl = request.url
          return HttpResponse.json({ queued: 1 }, { status: 202 })
        }),
      )

      const result = await reindexSession('sess-123')
      expect(result).toEqual({ queued: 1 })
      expect(capturedUrl).toContain('sessionId=sess-123')
    })

    it('throws on failure', async () => {
      server.use(
        http.post('/api/admin/reindex', () =>
          new HttpResponse('Server error', { status: 500 }),
        ),
      )

      await expect(reindexSession('sess-err')).rejects.toThrow('API 500')
    })
  })
})
