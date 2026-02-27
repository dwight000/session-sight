import { useMutation, useQueryClient } from '@tanstack/react-query'
import { uploadDocument, triggerExtraction } from '../api/upload'

interface UploadAndExtractParams {
  sessionId: string
  file: File
}

interface UploadAndExtractResult {
  documentId: string
  accepted: true
  sessionId: string
}

export function useUploadDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ sessionId, file }: UploadAndExtractParams): Promise<UploadAndExtractResult> => {
      // Step 1: Upload the document
      const uploadResult = await uploadDocument(sessionId, file)

      // Step 2: Trigger extraction (returns 202 — processing runs in background)
      const extractionResult = await triggerExtraction(sessionId)

      return {
        documentId: uploadResult.documentId,
        accepted: extractionResult.accepted,
        sessionId: extractionResult.sessionId,
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
      queryClient.invalidateQueries({ queryKey: ['reviewQueue'] })
    },
  })
}
