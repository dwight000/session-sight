import { fetchApi } from './client'
import type { PracticeSummary } from '../types'
import type { PatientRiskTrend } from '../types/riskTrend'
import type { PatientTimeline } from '../types/timeline'

export function getPracticeSummary(startDate: string, endDate: string): Promise<PracticeSummary> {
  return fetchApi<PracticeSummary>(
    `/api/summary/practice?startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`
  )
}

export function getPatientRiskTrend(patientId: string, startDate: string, endDate: string): Promise<PatientRiskTrend> {
  return fetchApi<PatientRiskTrend>(
    `/api/summary/patient/${encodeURIComponent(patientId)}/risk-trend?startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`
  )
}

export function getPatientTimeline(patientId: string, startDate?: string, endDate?: string): Promise<PatientTimeline> {
  const qs = new URLSearchParams()
  if (startDate) qs.set('startDate', startDate)
  if (endDate) qs.set('endDate', endDate)
  const query = qs.toString()

  return fetchApi<PatientTimeline>(
    `/api/summary/patient/${encodeURIComponent(patientId)}/timeline${query ? `?${query}` : ''}`,
  )
}
