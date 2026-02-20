import { useQuery } from '@tanstack/react-query'
import { getPatientSummary } from '../api/summary'

export function usePatientSummary(patientId: string, startDate?: string, endDate?: string) {
  return useQuery({
    queryKey: ['patientSummary', patientId, startDate, endDate],
    queryFn: () => getPatientSummary(patientId, startDate, endDate),
    enabled: !!patientId,
    retry: false,
  })
}
