import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useTherapists } from '../../src/hooks/useTherapists'
import { mockTherapists } from '../../src/test/fixtures/therapists'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useTherapists', () => {
  it('fetches therapists successfully', async () => {
    const { result } = renderHook(() => useTherapists(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockTherapists)
  })

  it('handles error', async () => {
    server.use(
      http.get('/api/therapists', () => HttpResponse.json({ error: 'Server error' }, { status: 500 }))
    )

    const { result } = renderHook(() => useTherapists(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
