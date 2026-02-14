import { useState } from 'react'
import { useTherapists } from '../hooks/useTherapists'
import { useCreateTherapist } from '../hooks/useCreateTherapist'
import { Button } from '../components/ui/Button'
import { Badge } from '../components/ui/Badge'
import { Spinner } from '../components/ui/Spinner'
import type { Therapist } from '../types'

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString()
}

export function Therapists() {
  const { data: therapists, isLoading, error } = useTherapists()
  const createTherapist = useCreateTherapist()
  const [showForm, setShowForm] = useState(false)
  const [formData, setFormData] = useState({
    name: '',
    licenseNumber: '',
    credentials: '',
    isActive: true,
  })

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    createTherapist.mutate(formData, {
      onSuccess: () => {
        setShowForm(false)
        setFormData({ name: '', licenseNumber: '', credentials: '', isActive: true })
      },
    })
  }

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load therapists: {(error as Error).message}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-900">Therapists</h2>
        <Button onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Cancel' : 'Add Therapist'}
        </Button>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} className="rounded-lg border border-gray-200 bg-white p-4 space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="name" className="block text-sm font-medium text-gray-700">Name</label>
              <input
                id="name"
                type="text"
                required
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="licenseNumber" className="block text-sm font-medium text-gray-700">License Number</label>
              <input
                id="licenseNumber"
                type="text"
                value={formData.licenseNumber}
                onChange={(e) => setFormData({ ...formData, licenseNumber: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div>
              <label htmlFor="credentials" className="block text-sm font-medium text-gray-700">Credentials</label>
              <input
                id="credentials"
                type="text"
                value={formData.credentials}
                onChange={(e) => setFormData({ ...formData, credentials: e.target.value })}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <div className="flex items-center gap-2 pt-6">
              <input
                id="isActive"
                type="checkbox"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <label htmlFor="isActive" className="text-sm font-medium text-gray-700">Active</label>
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={createTherapist.isPending}>
              {createTherapist.isPending ? 'Creating...' : 'Create Therapist'}
            </Button>
          </div>
          {createTherapist.isError && (
            <div className="text-sm text-red-600">
              {(createTherapist.error as Error).message}
            </div>
          )}
        </form>
      )}

      {therapists?.length === 0 ? (
        <div className="rounded-md bg-gray-50 p-8 text-center text-sm text-gray-500">
          No therapists yet. Click &quot;Add Therapist&quot; to create one.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr className="text-left text-xs font-medium uppercase text-gray-500">
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">License Number</th>
                <th className="px-4 py-3">Credentials</th>
                <th className="px-4 py-3">Active</th>
                <th className="px-4 py-3">Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {therapists?.map((therapist: Therapist) => (
                <tr key={therapist.id} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">
                    {therapist.name}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {therapist.licenseNumber || '\u2014'}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {therapist.credentials || '\u2014'}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <Badge variant={therapist.isActive ? 'approved' : 'dismissed'}>
                      {therapist.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {formatDate(therapist.createdAt)}
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
