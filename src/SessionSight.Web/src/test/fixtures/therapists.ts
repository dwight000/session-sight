import type { Therapist } from '../../types'

export const mockTherapists: Therapist[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Default Therapist',
    licenseNumber: null,
    credentials: null,
    isActive: true,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: null,
  },
  {
    id: 't2',
    name: 'Dr. Jane Wilson',
    licenseNumber: 'LIC-5678',
    credentials: 'PhD',
    isActive: true,
    createdAt: '2025-01-02T00:00:00Z',
    updatedAt: '2025-01-02T00:00:00Z',
  },
]
