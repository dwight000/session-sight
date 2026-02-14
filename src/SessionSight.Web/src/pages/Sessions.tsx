import { useState } from 'react'
import { useSessions } from '../hooks/useSessions'
import { usePatients } from '../hooks/usePatients'
import { useCreateSession } from '../hooks/useCreateSession'
import { useTherapists } from '../hooks/useTherapists'
import { Button } from '../components/ui/Button'
import { Badge } from '../components/ui/Badge'
import { Spinner } from '../components/ui/Spinner'
import type { Session, SessionType, SessionModality } from '../types'

const SESSION_TYPES: SessionType[] = ['Intake', 'Individual', 'Group', 'Family', 'Couples', 'Crisis', 'Assessment', 'Termination']
const SESSION_MODALITIES: SessionModality[] = ['InPerson', 'TelehealthVideo', 'TelehealthPhone', 'Hybrid']

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

function formatModality(modality: SessionModality): string {
  switch (modality) {
    case 'InPerson': return 'In-Person'
    case 'TelehealthVideo': return 'Telehealth (Video)'
    case 'TelehealthPhone': return 'Telehealth (Phone)'
    case 'Hybrid': return 'Hybrid'
    default: return modality
  }
}

export function Sessions() {
  const [patientFilter, setPatientFilter] = useState<string>('')
  const { data: sessions, isLoading, error } = useSessions(
    patientFilter ? { patientId: patientFilter } : undefined
  )
  const { data: patients } = usePatients()
  const { data: therapists } = useTherapists()
  const createSession = useCreateSession()
  const [showForm, setShowForm] = useState(false)
  const [formData, setFormData] = useState({
    patientId: '',
    therapistId: '',
    sessionDate: '',
    sessionType: 'Individual' as SessionType,
    modality: 'InPerson' as SessionModality,
    sessionNumber: 1,
  })

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    createSession.mutate(
      {
        ...formData,
        therapistId: formData.therapistId,
      },
      {
        onSuccess: () => {
          setShowForm(false)
          setFormData({
            patientId: '',
            therapistId: '',
            sessionDate: '',
            sessionType: 'Individual',
            modality: 'InPerson',
            sessionNumber: 1,
          })
        },
      }
    )
  }

  // Build patient name lookup
  const patientNames = new Map<string, string>()
  patients?.forEach((p) => {
    patientNames.set(p.id, `${p.firstName} ${p.lastName}`)
  })

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load sessions: {(error as Error).message}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-900">Sessions</h2>
        <div className="flex items-center gap-4">
          <select
            value={patientFilter}
            onChange={(e) => setPatientFilter(e.target.value)}
            className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="">All Patients</option>
            {patients?.map((patient) => (
              <option key={patient.id} value={patient.id}>
                {patient.firstName} {patient.lastName}
              </option>
            ))}
          </select>
          <Button onClick={() => setShowForm(!showForm)}>
            {showForm ? 'Cancel' : 'Add Session'}
          </Button>
        </div>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} className="rounded-lg border border-gray-200 bg-white p-4 space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <label htmlFor="patientId" className="block text-sm font-medium text-gray-700">Patient</label>
              <select
                id="patientId"
                required
                value={formData.patientId}
                onChange={(e) => setFormData({ ...formData, patientId: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              >
                <option value="">Select a patient...</option>
                {patients?.map((patient) => (
                  <option key={patient.id} value={patient.id}>
                    {patient.firstName} {patient.lastName}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="therapistId" className="block text-sm font-medium text-gray-700">Therapist</label>
              <select
                id="therapistId"
                required
                value={formData.therapistId}
                onChange={(e) => setFormData({ ...formData, therapistId: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              >
                <option value="">Select a therapist...</option>
                {therapists?.map((therapist) => (
                  <option key={therapist.id} value={therapist.id}>
                    {therapist.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="sessionDate" className="block text-sm font-medium text-gray-700">Session Date</label>
              <input
                id="sessionDate"
                type="date"
                required
                value={formData.sessionDate}
                onChange={(e) => setFormData({ ...formData, sessionDate: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="sessionType" className="block text-sm font-medium text-gray-700">Session Type</label>
              <select
                id="sessionType"
                required
                value={formData.sessionType}
                onChange={(e) => setFormData({ ...formData, sessionType: e.target.value as SessionType })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              >
                {SESSION_TYPES.map((type) => (
                  <option key={type} value={type}>{type}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="modality" className="block text-sm font-medium text-gray-700">Modality</label>
              <select
                id="modality"
                required
                value={formData.modality}
                onChange={(e) => setFormData({ ...formData, modality: e.target.value as SessionModality })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              >
                {SESSION_MODALITIES.map((modality) => (
                  <option key={modality} value={modality}>{formatModality(modality)}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="sessionNumber" className="block text-sm font-medium text-gray-700">Session Number</label>
              <input
                id="sessionNumber"
                type="number"
                required
                min="1"
                value={formData.sessionNumber}
                onChange={(e) => setFormData({ ...formData, sessionNumber: parseInt(e.target.value) || 1 })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={createSession.isPending}>
              {createSession.isPending ? 'Creating...' : 'Create Session'}
            </Button>
          </div>
          {createSession.isError && (
            <div className="text-sm text-red-600">
              {(createSession.error as Error).message}
            </div>
          )}
        </form>
      )}

      {sessions?.length === 0 ? (
        <div className="rounded-md bg-gray-50 p-8 text-center text-sm text-gray-500">
          No sessions found. Click "Add Session" to create one.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr className="text-left text-xs font-medium uppercase text-gray-500">
                <th className="px-4 py-3">Patient</th>
                <th className="px-4 py-3">Date</th>
                <th className="px-4 py-3">Type</th>
                <th className="px-4 py-3">Modality</th>
                <th className="px-4 py-3">#</th>
                <th className="px-4 py-3">Document</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {sessions?.map((session: Session) => (
                <tr key={session.id} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">
                    {patientNames.get(session.patientId) || 'Unknown'}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {formatDate(session.sessionDate)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {session.sessionType}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {formatModality(session.modality)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {session.sessionNumber}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <Badge variant={session.hasDocument ? 'approved' : 'pending'}>
                      {session.hasDocument ? 'Uploaded' : 'No Document'}
                    </Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
