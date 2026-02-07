import { fetchApi } from './client'
import type { PracticeSummary } from '../types'

export function getPracticeSummary(startDate: string, endDate: string): Promise<PracticeSummary> {
  return fetchApi<PracticeSummary>(
    `/api/summary/practice?startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`
  )
}
