import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useRegenerateSessionSummary } from '../../src/hooks/useRegenerateSessionSummary'
import { mockSessionSummary } from '../../src/test/fixtures/summary'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useRegenerateSessionSummary', () => {
  it('calls GET with ?regenerate=true', async () => {
    let capturedUrl = ''
    server.use(
      http.get('/api/summary/session/:sessionId', ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json(mockSessionSummary)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useRegenerateSessionSummary('sess-001'), { wrapper })

    result.current.mutate()

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedUrl).toContain('regenerate=true')
  })

  it('sets error state on failure', async () => {
    server.use(
      http.get('/api/summary/session/:sessionId', () => {
        return new HttpResponse('Server Error', { status: 500 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useRegenerateSessionSummary('sess-err'), { wrapper })

    result.current.mutate()

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
