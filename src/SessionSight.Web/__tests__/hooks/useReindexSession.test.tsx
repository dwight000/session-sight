import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useReindexSession } from '../../src/hooks/useReindexSession'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useReindexSession', () => {
  it('sends POST to reindex endpoint with sessionId', async () => {
    let capturedUrl = ''
    server.use(
      http.post('/api/admin/reindex', ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ queued: 1 }, { status: 202 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useReindexSession(), { wrapper })

    result.current.mutate('sess-reindex-001')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedUrl).toContain('sessionId=sess-reindex-001')
  })

  it('sets error state on failure', async () => {
    server.use(
      http.post('/api/admin/reindex', () =>
        new HttpResponse('Internal error', { status: 500 }),
      ),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useReindexSession(), { wrapper })

    result.current.mutate('sess-reindex-err')

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
