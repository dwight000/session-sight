import { fetchApi } from './client'
import type { PracticeSummary } from '../types'
import type { PatientRiskTrend } from '../types/riskTrend'

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
