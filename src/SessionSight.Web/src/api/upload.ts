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

export interface ExtractionResult {
  success: boolean
  errorMessage?: string
  extractionId?: string
}

export async function triggerExtraction(sessionId: string): Promise<ExtractionResult> {
  const res = await fetch(`/api/extraction/${sessionId}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(`Extraction failed (${res.status}): ${text}`)
  }

  return res.json()
}
