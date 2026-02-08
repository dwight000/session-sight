import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { getSessions, getSession, createSession } from '../../src/api/sessions'

describe('sessions api', () => {
  describe('getSessions', () => {
    it('fetches all sessions without filters', async () => {
      const sessions = [{ id: '1', patientId: 'p1', sessionDate: '2025-01-01' }]
      server.use(
        http.get('/api/sessions', () => HttpResponse.json(sessions))
      )

      const result = await getSessions()
      expect(result).toEqual(sessions)
    })

    it('fetches sessions with patient filter', async () => {
      const sessions = [{ id: '1', patientId: 'p1' }]
      server.use(
        http.get('/api/sessions', ({ request }) => {
          const url = new URL(request.url)
          if (url.searchParams.get('patientId') === 'p1') {
            return HttpResponse.json(sessions)
          }
          return HttpResponse.json([])
        })
      )

      const result = await getSessions({ patientId: 'p1' })
      expect(result).toEqual(sessions)
    })

    it('fetches sessions with hasDocument filter', async () => {
      const sessions = [{ id: '1', patientId: 'p1', hasDocument: false }]
      server.use(
        http.get('/api/sessions', ({ request }) => {
          const url = new URL(request.url)
          if (url.searchParams.get('hasDocument') === 'false') {
            return HttpResponse.json(sessions)
          }
          return HttpResponse.json([])
        })
      )

      const result = await getSessions({ hasDocument: false })
      expect(result).toEqual(sessions)
    })
  })

  describe('getSession', () => {
    it('fetches a single session', async () => {
      const session = { id: '1', patientId: 'p1' }
      server.use(
        http.get('/api/sessions/1', () => HttpResponse.json(session))
      )

      const result = await getSession('1')
      expect(result).toEqual(session)
    })
  })

  describe('createSession', () => {
    it('creates a new session', async () => {
      const request = {
        patientId: 'p1',
        therapistId: 't1',
        sessionDate: '2025-01-01',
        sessionType: 'Individual' as const,
        modality: 'InPerson' as const,
        sessionNumber: 1
      }
      const created = { id: '1', ...request }
      server.use(
        http.post('/api/sessions', () => HttpResponse.json(created))
      )

      const result = await createSession(request)
      expect(result).toEqual(created)
    })
  })
})
