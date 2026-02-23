import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useExtractionResult } from '../../src/hooks/useExtractionResult'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useExtractionResult', () => {
  it('fetches extraction result when enabled', async () => {
    const { result } = renderHook(() => useExtractionResult('sess-001', true), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.id).toBe('ext-001')
    expect(result.current.data?.riskDiagnostics?.fieldDecisions).toHaveLength(2)
  })

  it('does not fetch when enabled is false', () => {
    const { result } = renderHook(() => useExtractionResult('sess-001', false), { wrapper: createWrapper() })

    expect(result.current.fetchStatus).toBe('idle')
  })

  it('does not fetch when sessionId is empty', () => {
    const { result } = renderHook(() => useExtractionResult('', true), { wrapper: createWrapper() })

    expect(result.current.fetchStatus).toBe('idle')
  })

  it('handles 404 gracefully', async () => {
    server.use(
      // Override extraction/steps so it doesn't match before our handler
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json({ extractionId: 'ext-x', documentStatus: null, steps: [] })
      }),
      http.get('/api/sessions/:sessionId/extraction', () => {
        return new HttpResponse('API 404: Not Found', { status: 404 })
      }),
    )

    const { result } = renderHook(() => useExtractionResult('nonexistent', true), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 5000 })
  })
})
