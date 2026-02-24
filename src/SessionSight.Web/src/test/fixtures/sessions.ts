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
    documentStatus: null,
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
    documentStatus: 'Completed',
    createdAt: '2025-01-02T00:00:00Z',
    updatedAt: '2025-01-02T00:00:00Z'
  },
  {
    id: 's3',
    patientId: 'p1',
    therapistId: 't1',
    sessionDate: '2025-01-29',
    sessionType: 'Individual',
    modality: 'InPerson',
    durationMinutes: 50,
    sessionNumber: 3,
    hasDocument: true,
    documentStatus: 'Failed',
    createdAt: '2025-01-03T00:00:00Z',
    updatedAt: '2025-01-03T00:00:00Z'
  },
  {
    id: 's4',
    patientId: 'p1',
    therapistId: 't1',
    sessionDate: '2025-02-05',
    sessionType: 'Individual',
    modality: 'InPerson',
    durationMinutes: 50,
    sessionNumber: 4,
    hasDocument: true,
    documentStatus: 'PartiallyCompleted',
    createdAt: '2025-02-01T00:00:00Z',
    updatedAt: '2025-02-01T00:00:00Z'
  }
]
