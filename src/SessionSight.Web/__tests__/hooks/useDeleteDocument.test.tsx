import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useDeleteDocument } from '../../src/hooks/useDeleteDocument'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useDeleteDocument', () => {
  it('sends DELETE to correct URL', async () => {
    let capturedMethod = ''
    let capturedSessionId = ''
    server.use(
      http.delete('/api/sessions/:sessionId/document', ({ request, params }) => {
        capturedMethod = request.method
        capturedSessionId = params.sessionId as string
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useDeleteDocument('sess-del-001'), { wrapper })

    result.current.mutate()

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedMethod).toBe('DELETE')
    expect(capturedSessionId).toBe('sess-del-001')
  })

  it('sets error state on failure', async () => {
    server.use(
      http.delete('/api/sessions/:sessionId/document', () => {
        return new HttpResponse('Not Found', { status: 404 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useDeleteDocument('sess-del-err'), { wrapper })

    result.current.mutate()

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
