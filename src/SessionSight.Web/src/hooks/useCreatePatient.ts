import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createPatient } from '../api/patients'
import type { CreatePatientRequest } from '../types'

export function useCreatePatient() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CreatePatientRequest) => createPatient(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['patients'] })
    },
  })
}
