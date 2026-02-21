import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { usePatientSummary } from '../../src/hooks/usePatientSummary'
import { mockPatientSummary } from '../../src/test/fixtures/summary'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePatientSummary', () => {
  it('is disabled when patientId is empty', () => {
    const wrapper = createWrapper()
    const { result } = renderHook(() => usePatientSummary('', '2025-01-01', '2025-01-31'), { wrapper })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.data).toBeUndefined()
  })

  it('fetches data when patientId is provided', async () => {
    server.use(
      http.get('/api/summary/patient/:patientId', () => {
        return HttpResponse.json(mockPatientSummary)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => usePatientSummary('p1', '2025-01-01', '2025-01-31'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockPatientSummary)
  })

  it('fetches without date params', async () => {
    server.use(
      http.get('/api/summary/patient/:patientId', () => {
        return HttpResponse.json(mockPatientSummary)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => usePatientSummary('p1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(mockPatientSummary)
  })
})
