import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { usePatients } from '../../src/hooks/usePatients'
import { mockPatients } from '../../src/test/fixtures/patients'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePatients', () => {
  it('fetches patients successfully', async () => {
    const { result } = renderHook(() => usePatients(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockPatients)
  })

  it('handles error', async () => {
    server.use(
      http.get('/api/patients', () => HttpResponse.json({ error: 'Server error' }, { status: 500 }))
    )

    const { result } = renderHook(() => usePatients(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
