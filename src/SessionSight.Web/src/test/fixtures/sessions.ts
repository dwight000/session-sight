import type { Session } from '../../types'

export const mockSessions: Session[] = [
  {
    id: 's1',
    patientId: 'p1',
    therapistId: 't1',
    sessionDate: '2025-01-15',
    sessionType: 'Individual',
    modality: 'InPerson',
    durationMinutes: 50,
    sessionNumber: 1,
    hasDocument: false,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z'
  },
  {
    id: 's2',
    patientId: 'p1',
    therapistId: 't1',
    sessionDate: '2025-01-22',
    sessionType: 'Individual',
    modality: 'TelehealthVideo',
    durationMinutes: 50,
    sessionNumber: 2,
    hasDocument: true,
    createdAt: '2025-01-02T00:00:00Z',
    updatedAt: '2025-01-02T00:00:00Z'
  }
]
