import { useQuery } from '@tanstack/react-query'
import { getSessions, type GetSessionsParams } from '../api/sessions'

export function useSessions(params?: GetSessionsParams) {
  return useQuery({
    queryKey: ['sessions', params?.patientId, params?.hasDocument],
    queryFn: () => getSessions(params),
  })
}
