import type { UploadDocumentResponse } from '../types'

// Note: This uses raw fetch instead of fetchApi because FormData requires no Content-Type header
// (browser sets it automatically with boundary)
export async function uploadDocument(sessionId: string, file: File): Promise<UploadDocumentResponse> {
  const formData = new FormData()
  formData.append('file', file)

  const res = await fetch(`/api/sessions/${sessionId}/document`, {
    method: 'POST',
    body: formData,
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(`Upload failed (${res.status}): ${text}`)
  }

  return res.json()
}

export async function deleteDocument(sessionId: string): Promise<void> {
  const res = await fetch(`/api/sessions/${sessionId}/document`, {
    method: 'DELETE',
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(`Delete failed (${res.status}): ${text}`)
  }
}

export interface ExtractionAccepted {
  accepted: true
  sessionId: string
}

export async function triggerExtraction(sessionId: string): Promise<ExtractionAccepted> {
  const res = await fetch(`/api/extraction/${sessionId}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(`Extraction failed (${res.status}): ${text}`)
  }

  // Server returns 202 Accepted with { sessionId }
  const body = await res.json()
  return { accepted: true, sessionId: body.sessionId ?? sessionId }
}
