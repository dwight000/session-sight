import { describe, it, expect, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useCreateTherapist } from '../../src/hooks/useCreateTherapist'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })
  return { Wrapper, queryClient }

  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('useCreateTherapist', () => {
  it('creates therapist successfully', async () => {
    const { Wrapper } = createWrapper()
    const { result } = renderHook(() => useCreateTherapist(), { wrapper: Wrapper })

    result.current.mutate({
      name: 'Dr. Test',
      licenseNumber: 'LIC-0001',
      credentials: 'PhD',
      isActive: true,
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveProperty('id')
  })

  it('invalidates therapists cache on success', async () => {
    const { Wrapper, queryClient } = createWrapper()
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')

    const { result } = renderHook(() => useCreateTherapist(), { wrapper: Wrapper })

    result.current.mutate({
      name: 'Dr. Cache Test',
      isActive: true,
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['therapists'] })
  })
})
