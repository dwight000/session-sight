import { useMutation, useQueryClient } from '@tanstack/react-query'
import { uploadDocument, triggerExtraction } from '../api/upload'

interface UploadAndExtractParams {
  sessionId: string
  file: File
}

interface UploadAndExtractResult {
  documentId: string
  extractionId?: string
  success: boolean
  errorMessage?: string
}

export function useUploadDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ sessionId, file }: UploadAndExtractParams): Promise<UploadAndExtractResult> => {
      // Step 1: Upload the document
      const uploadResult = await uploadDocument(sessionId, file)

      // Step 2: Trigger extraction
      const extractionResult = await triggerExtraction(sessionId)

      return {
        documentId: uploadResult.documentId,
        extractionId: extractionResult.extractionId,
        success: extractionResult.success,
        errorMessage: extractionResult.errorMessage,
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
      queryClient.invalidateQueries({ queryKey: ['reviewQueue'] })
    },
  })
}
