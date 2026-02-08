import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { uploadDocument, triggerExtraction } from '../../src/api/upload'

describe('upload api', () => {
  describe('uploadDocument', () => {
    it('uploads a document successfully', async () => {
      const response = { documentId: 'd1', sessionId: 's1', fileName: 'test.pdf', blobUri: 'blob://test', status: 'Pending' }
      server.use(
        http.post('/api/sessions/s1/document', () => HttpResponse.json(response))
      )

      const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
      const result = await uploadDocument('s1', file)

      expect(result).toEqual(response)
    })

    it('throws on upload failure', async () => {
      server.use(
        http.post('/api/sessions/s1/document', () =>
          new HttpResponse('Document already exists', { status: 409 })
        )
      )

      const file = new File(['test'], 'test.pdf')
      await expect(uploadDocument('s1', file)).rejects.toThrow('Upload failed (409)')
    })
  })

  describe('triggerExtraction', () => {
    it('triggers extraction for a session', async () => {
      const response = { success: true, extractionId: 'e1' }
      server.use(
        http.post('/api/extraction/s1', () => HttpResponse.json(response))
      )

      const result = await triggerExtraction('s1')
      expect(result).toEqual(response)
    })

    it('throws on extraction failure', async () => {
      server.use(
        http.post('/api/extraction/s1', () =>
          new HttpResponse('Session has no document', { status: 400 })
        )
      )

      await expect(triggerExtraction('s1')).rejects.toThrow('Extraction failed (400)')
    })
  })
})
