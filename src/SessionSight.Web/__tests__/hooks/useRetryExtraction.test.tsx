import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useRetryExtraction } from '../../src/hooks/useRetryExtraction'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useRetryExtraction', () => {
  it('calls triggerExtraction with correct sessionId', async () => {
    let capturedSessionId = ''
    server.use(
      http.post('/api/extraction/:sessionId', ({ params }) => {
        capturedSessionId = params.sessionId as string
        return HttpResponse.json({ success: true, extractionId: 'ext-1' })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useRetryExtraction(), { wrapper })

    result.current.mutate('sess-abc')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedSessionId).toBe('sess-abc')
  })

  it('invalidates sessions cache on success', async () => {
    server.use(
      http.post('/api/extraction/:sessionId', () => {
        return HttpResponse.json({ success: true, extractionId: 'ext-2' })
      }),
    )

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })

    queryClient.setQueryData(['sessions'], [{ old: true }])

    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    }

    const { result } = renderHook(() => useRetryExtraction(), { wrapper: Wrapper })

    result.current.mutate('sess-xyz')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queryClient.getQueryState(['sessions'])?.isInvalidated).toBe(true)
  })

  it('sets error state on failure', async () => {
    server.use(
      http.post('/api/extraction/:sessionId', () => {
        return new HttpResponse('Conflict', { status: 409 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useRetryExtraction(), { wrapper })

    result.current.mutate('sess-fail')

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
