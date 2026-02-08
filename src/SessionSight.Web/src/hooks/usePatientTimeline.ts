import { useQuery } from '@tanstack/react-query'
import { getPatientTimeline } from '../api/summary'

export function usePatientTimeline(patientId: string, startDate?: string, endDate?: string) {
  return useQuery({
    queryKey: ['patientTimeline', patientId, startDate, endDate],
    queryFn: () => getPatientTimeline(patientId, startDate, endDate),
    enabled: !!patientId,
  })
}
