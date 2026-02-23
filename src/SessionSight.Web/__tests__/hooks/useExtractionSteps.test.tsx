import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useExtractionSteps } from '../../src/hooks/useExtractionSteps'
import { mockExtractionStepsPartial } from '../../src/test/fixtures/extractionSteps'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useExtractionSteps', () => {
  it('fetches extraction steps successfully', async () => {
    const { result } = renderHook(() => useExtractionSteps('sess-001', false), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.extractionId).toBe('ext-001')
    expect(result.current.data?.steps).toHaveLength(6)
  })

  it('does not fetch when sessionId is empty', () => {
    const { result } = renderHook(() => useExtractionSteps('', false), { wrapper: createWrapper() })

    expect(result.current.fetchStatus).toBe('idle')
  })

  it('does not poll in historical mode', async () => {
    const { result } = renderHook(() => useExtractionSteps('sess-001', false), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    // refetchInterval should be false (no polling) — data is returned and isLive=false
    expect(result.current.data?.steps).toHaveLength(6)
  })

  it('handles 404 gracefully', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return new HttpResponse('API 404: Not Found', { status: 404 })
      }),
    )

    const { result } = renderHook(() => useExtractionSteps('nonexistent', false), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })

  it('returns partial data in live mode', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsPartial)
      }),
    )

    const { result } = renderHook(() => useExtractionSteps('sess-001', true), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.steps).toHaveLength(2)
  })
})
