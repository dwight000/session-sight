import { useQuery } from '@tanstack/react-query'
import { getTherapists } from '../api/therapists'

export function useTherapists() {
  return useQuery({
    queryKey: ['therapists'],
    queryFn: getTherapists,
  })
}
