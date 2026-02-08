import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { getPatients, getPatient, createPatient } from '../../src/api/patients'

describe('patients api', () => {
  describe('getPatients', () => {
    it('fetches all patients', async () => {
      const patients = [
        { id: '1', firstName: 'John', lastName: 'Doe', dateOfBirth: '1990-01-01', externalId: 'EXT1' }
      ]
      server.use(
        http.get('/api/patients', () => HttpResponse.json(patients))
      )

      const result = await getPatients()
      expect(result).toEqual(patients)
    })
  })

  describe('getPatient', () => {
    it('fetches a single patient', async () => {
      const patient = { id: '1', firstName: 'John', lastName: 'Doe' }
      server.use(
        http.get('/api/patients/1', () => HttpResponse.json(patient))
      )

      const result = await getPatient('1')
      expect(result).toEqual(patient)
    })
  })

  describe('createPatient', () => {
    it('creates a new patient', async () => {
      const request = { firstName: 'John', lastName: 'Doe', dateOfBirth: '1990-01-01', externalId: 'EXT1' }
      const created = { id: '1', ...request }
      server.use(
        http.post('/api/patients', () => HttpResponse.json(created))
      )

      const result = await createPatient(request)
      expect(result).toEqual(created)
    })
  })
})
