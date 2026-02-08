import { useQuery } from '@tanstack/react-query'
import { getPatients } from '../api/patients'

export function usePatients() {
  return useQuery({
    queryKey: ['patients'],
    queryFn: getPatients,
  })
}
