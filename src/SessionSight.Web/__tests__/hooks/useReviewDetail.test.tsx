import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useReviewDetail } from '../../src/hooks/useReviewDetail'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useReviewDetail', () => {
  it('is disabled when sessionId is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => useReviewDetail(''), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('fetches data when sessionId is provided', async () => {
    const mockDetail = { sessionId: 'sess-123', status: 'Pending', patientName: 'Test Patient' }
    server.use(
      http.get('/api/review/session/:sessionId', () => {
        return HttpResponse.json(mockDetail)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useReviewDetail('sess-123'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockDetail)
  })
})
