import { fetchApi } from './client'
import type { Session, CreateSessionRequest } from '../types'

export interface GetSessionsParams {
  patientId?: string
  hasDocument?: boolean
}

export function getSessions(params?: GetSessionsParams): Promise<Session[]> {
  const qs = new URLSearchParams()
  if (params?.patientId) qs.set('patientId', params.patientId)
  if (params?.hasDocument !== undefined) qs.set('hasDocument', String(params.hasDocument))
  const query = qs.toString()
  return fetchApi<Session[]>(`/api/sessions${query ? `?${query}` : ''}`)
}

export function getSession(id: string): Promise<Session> {
  return fetchApi<Session>(`/api/sessions/${id}`)
}

export function getSessionsByPatient(patientId: string): Promise<Session[]> {
  return fetchApi<Session[]>(`/api/patients/${patientId}/sessions`)
}

export function createSession(request: CreateSessionRequest): Promise<Session> {
  return fetchApi<Session>('/api/sessions', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
