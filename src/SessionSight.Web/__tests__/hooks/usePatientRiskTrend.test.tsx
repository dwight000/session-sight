import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { usePatientRiskTrend } from '../../src/hooks/usePatientRiskTrend'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePatientRiskTrend', () => {
  it('is disabled when patientId is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => usePatientRiskTrend('', '2025-01-01', '2025-01-31'), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('fetches data when all params are provided', async () => {
    const mockTrend = { totalSessions: 4, points: [] }
    server.use(
      http.get('/api/summary/patient/:patientId/risk-trend', () => {
        return HttpResponse.json(mockTrend)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(
      () => usePatientRiskTrend('pat-001', '2025-01-01', '2025-01-31'),
      { wrapper },
    )

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockTrend)
  })
})
