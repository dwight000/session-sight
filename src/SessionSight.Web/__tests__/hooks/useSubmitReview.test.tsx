import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useSubmitReview } from '../../src/hooks/useSubmitReview'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useSubmitReview', () => {
  it('sends POST with correct body', async () => {
    let capturedBody: unknown = null
    server.use(
      http.post('/api/review/session/:sessionId', async ({ request }) => {
        capturedBody = await request.json()
        return HttpResponse.json(null, { status: 200 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useSubmitReview('sess-123'), { wrapper })

    result.current.mutate({ action: 'Approved', reviewerName: 'Dr. Smith' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedBody).toEqual({ action: 'Approved', reviewerName: 'Dr. Smith' })
  })

  it('posts to the correct sessionId URL', async () => {
    let capturedPath = ''
    server.use(
      http.post('/api/review/session/:sessionId', ({ params }) => {
        capturedPath = params.sessionId as string
        return HttpResponse.json(null, { status: 200 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useSubmitReview('sess-456'), { wrapper })

    result.current.mutate({ action: 'Dismissed', reviewerName: 'Dr. Jones' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedPath).toBe('sess-456')
  })

  it('invalidates reviewDetail, reviewQueue, and reviewStats caches on success', async () => {
    server.use(
      http.post('/api/review/session/:sessionId', () => {
        return HttpResponse.json(null, { status: 200 })
      }),
    )

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })

    // Seed caches
    queryClient.setQueryData(['reviewDetail', 'sess-789'], { old: true })
    queryClient.setQueryData(['reviewQueue'], [{ old: true }])
    queryClient.setQueryData(['reviewStats'], { old: true })

    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    }

    const { result } = renderHook(() => useSubmitReview('sess-789'), { wrapper: Wrapper })

    result.current.mutate({ action: 'Approved', reviewerName: 'Dr. Test' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    // After invalidation, all three cache entries should be marked stale (invalidated)
    expect(queryClient.getQueryState(['reviewDetail', 'sess-789'])?.isInvalidated).toBe(true)
    expect(queryClient.getQueryState(['reviewQueue'])?.isInvalidated).toBe(true)
    expect(queryClient.getQueryState(['reviewStats'])?.isInvalidated).toBe(true)
  })

  it('sets error state on failure', async () => {
    server.use(
      http.post('/api/review/session/:sessionId', () => {
        return new HttpResponse('Forbidden', { status: 403 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useSubmitReview('sess-err'), { wrapper })

    result.current.mutate({ action: 'Approved', reviewerName: 'Dr. Fail' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
