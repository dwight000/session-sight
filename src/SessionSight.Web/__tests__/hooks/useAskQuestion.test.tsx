import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { useAskQuestion } from '../../src/hooks/useAskQuestion'
import { mockQAResponse } from '../../src/test/fixtures/qa'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useAskQuestion', () => {
  it('sends POST with correct body', async () => {
    let capturedBody: Record<string, unknown> | null = null
    server.use(
      http.post('/api/qa/patient/:patientId', async ({ request }) => {
        capturedBody = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(mockQAResponse)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useAskQuestion('p1'), { wrapper })

    result.current.mutate({ question: 'What concerns?' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedBody).toEqual({ question: 'What concerns?' })
  })

  it('posts to the correct patientId URL', async () => {
    let capturedPatientId: string | null = null
    server.use(
      http.post('/api/qa/patient/:patientId', ({ params }) => {
        capturedPatientId = params.patientId as string
        return HttpResponse.json(mockQAResponse)
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useAskQuestion('patient-abc'), { wrapper })

    result.current.mutate({ question: 'Any concerns?' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(capturedPatientId).toBe('patient-abc')
  })

  it('sets error state on HTTP failure', async () => {
    server.use(
      http.post('/api/qa/patient/:patientId', () => {
        return new HttpResponse('Patient not found', { status: 404 })
      }),
    )

    const wrapper = createWrapper()
    const { result } = renderHook(() => useAskQuestion('p1'), { wrapper })

    result.current.mutate({ question: 'What concerns?' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toBeDefined()
  })
})
