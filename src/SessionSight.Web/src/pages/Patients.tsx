import { useState } from 'react'
import { Link } from 'react-router-dom'
import { usePatients } from '../hooks/usePatients'
import { useCreatePatient } from '../hooks/useCreatePatient'
import { Button } from '../components/ui/Button'
import { Spinner } from '../components/ui/Spinner'
import type { Patient } from '../types'

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString()
}

export function Patients() {
  const { data: patients, isLoading, error } = usePatients()
  const createPatient = useCreatePatient()
  const [showForm, setShowForm] = useState(false)
  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    dateOfBirth: '',
    externalId: '',
  })

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    createPatient.mutate(formData, {
      onSuccess: () => {
        setShowForm(false)
        setFormData({ firstName: '', lastName: '', dateOfBirth: '', externalId: '' })
      },
    })
  }

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load patients: {(error as Error).message}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-900">Patients</h2>
        <Button onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Cancel' : 'Add Patient'}
        </Button>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} className="rounded-lg border border-gray-200 bg-white p-4 space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="firstName" className="block text-sm font-medium text-gray-700">First Name</label>
              <input
                id="firstName"
                type="text"
                required
                value={formData.firstName}
                onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="lastName" className="block text-sm font-medium text-gray-700">Last Name</label>
              <input
                id="lastName"
                type="text"
                required
                value={formData.lastName}
                onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="dateOfBirth" className="block text-sm font-medium text-gray-700">Date of Birth</label>
              <input
                id="dateOfBirth"
                type="date"
                required
                value={formData.dateOfBirth}
                onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="externalId" className="block text-sm font-medium text-gray-700">External ID</label>
              <input
                id="externalId"
                type="text"
                required
                value={formData.externalId}
                onChange={(e) => setFormData({ ...formData, externalId: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={createPatient.isPending}>
              {createPatient.isPending ? 'Creating...' : 'Create Patient'}
            </Button>
          </div>
          {createPatient.isError && (
            <div className="text-sm text-red-600">
              {(createPatient.error as Error).message}
            </div>
          )}
        </form>
      )}

      {patients?.length === 0 ? (
        <div className="rounded-md bg-gray-50 p-8 text-center text-sm text-gray-500">
          No patients yet. Click "Add Patient" to create one.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr className="text-left text-xs font-medium uppercase text-gray-500">
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">Date of Birth</th>
                <th className="px-4 py-3">External ID</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {patients?.map((patient: Patient) => (
                <tr key={patient.id} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">
                    {patient.firstName} {patient.lastName}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {formatDate(patient.dateOfBirth)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {patient.externalId}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {formatDate(patient.createdAt)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <Link to={`/patients/${patient.id}/timeline`}>
                      <Button variant="secondary" className="text-xs">
                        Timeline &rarr;
                      </Button>
                    </Link>
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
