import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createTherapist } from '../api/therapists'
import type { CreateTherapistRequest } from '../types'

export function useCreateTherapist() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CreateTherapistRequest) => createTherapist(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['therapists'] })
    },
  })
}
