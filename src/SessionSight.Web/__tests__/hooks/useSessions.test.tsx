import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useSessions } from '../../src/hooks/useSessions'
import { mockSessions } from '../../src/test/fixtures/sessions'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useSessions', () => {
  it('fetches sessions without filters', async () => {
    const { result } = renderHook(() => useSessions(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockSessions)
  })

  it('fetches sessions with patientId filter', async () => {
    server.use(
      http.get('/api/sessions', ({ request }) => {
        const url = new URL(request.url)
        const patientId = url.searchParams.get('patientId')
        if (patientId === 'p1') {
          return HttpResponse.json(mockSessions.filter(s => s.patientId === 'p1'))
        }
        return HttpResponse.json([])
      })
    )

    const { result } = renderHook(() => useSessions({ patientId: 'p1' }), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.length).toBeGreaterThan(0)
  })
})
