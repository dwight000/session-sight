import { fetchApi } from './client'
import type { Patient, CreatePatientRequest } from '../types'

export function getPatients(): Promise<Patient[]> {
  return fetchApi<Patient[]>('/api/patients')
}

export function getPatient(id: string): Promise<Patient> {
  return fetchApi<Patient>(`/api/patients/${id}`)
}

export function createPatient(request: CreatePatientRequest): Promise<Patient> {
  return fetchApi<Patient>('/api/patients', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
