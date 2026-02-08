import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { usePatientTimeline } from '../../src/hooks/usePatientTimeline'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePatientTimeline', () => {
  it('is disabled when patientId is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => usePatientTimeline('', '2025-01-01', '2025-01-31'), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('fetches data when patientId is provided', async () => {
    const mockTimeline = { totalSessions: 4, entries: [] }
    server.use(
      http.get('/api/summary/patient/:patientId/timeline', () => {
        return HttpResponse.json(mockTimeline)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(
      () => usePatientTimeline('pat-001', '2025-01-01', '2025-01-31'),
      { wrapper },
    )

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockTimeline)
  })
})
