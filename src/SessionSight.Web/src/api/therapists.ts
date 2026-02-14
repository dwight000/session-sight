import { fetchApi } from './client'
import type { Therapist, CreateTherapistRequest } from '../types'

export function getTherapists(): Promise<Therapist[]> {
  return fetchApi<Therapist[]>('/api/therapists')
}

export function createTherapist(request: CreateTherapistRequest): Promise<Therapist> {
  return fetchApi<Therapist>('/api/therapists', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
