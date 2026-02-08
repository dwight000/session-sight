import type { Patient } from '../../types'

export const mockPatients: Patient[] = [
  {
    id: 'p1',
    firstName: 'John',
    lastName: 'Doe',
    dateOfBirth: '1990-01-15',
    externalId: 'EXT001',
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z'
  },
  {
    id: 'p2',
    firstName: 'Jane',
    lastName: 'Smith',
    dateOfBirth: '1985-05-20',
    externalId: 'EXT002',
    createdAt: '2025-01-02T00:00:00Z',
    updatedAt: '2025-01-02T00:00:00Z'
  }
]
