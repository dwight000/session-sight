import { useQuery } from '@tanstack/react-query'
import { getPracticeSummary } from '../api/summary'

export function usePracticeSummary(startDate: string, endDate: string) {
  return useQuery({
    queryKey: ['practiceSummary', startDate, endDate],
    queryFn: () => getPracticeSummary(startDate, endDate),
    enabled: !!startDate && !!endDate,
  })
}
