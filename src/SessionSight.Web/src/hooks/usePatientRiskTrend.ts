import { useQuery } from '@tanstack/react-query'
import { getPatientRiskTrend } from '../api/summary'

export function usePatientRiskTrend(patientId: string, startDate: string, endDate: string) {
  return useQuery({
    queryKey: ['patientRiskTrend', patientId, startDate, endDate],
    queryFn: () => getPatientRiskTrend(patientId, startDate, endDate),
    enabled: !!patientId && !!startDate && !!endDate,
  })
}
