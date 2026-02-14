import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useProcessingJobs } from '../../src/hooks/useProcessingJobs'
import { mockProcessingJobs } from '../../src/test/fixtures/processingJobs'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useProcessingJobs', () => {
  it('fetches processing jobs successfully', async () => {
    const { result } = renderHook(() => useProcessingJobs(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockProcessingJobs)
  })

  it('handles error', async () => {
    server.use(
      http.get('/api/processing-jobs', () => HttpResponse.json({ error: 'Server error' }, { status: 500 }))
    )

    const { result } = renderHook(() => useProcessingJobs(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
