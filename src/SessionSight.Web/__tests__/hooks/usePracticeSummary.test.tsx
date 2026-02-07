import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { usePracticeSummary } from '../../src/hooks/usePracticeSummary'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePracticeSummary', () => {
  it('is disabled when startDate is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => usePracticeSummary('', '2025-01-31'), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('is disabled when endDate is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => usePracticeSummary('2025-01-01', ''), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('fetches data when both dates are provided', async () => {
    const mockSummary = { totalSessions: 42, averageMood: 6.5 }
    server.use(
      http.get('/api/summary/practice', () => {
        return HttpResponse.json(mockSummary)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => usePracticeSummary('2025-01-01', '2025-01-31'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockSummary)
  })
})
