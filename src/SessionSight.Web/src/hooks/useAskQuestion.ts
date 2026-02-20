import { useMutation } from '@tanstack/react-query'
import { askQuestion } from '../api/qa'

export function useAskQuestion(patientId: string) {
  return useMutation({
    mutationFn: (body: { question: string }) => askQuestion(patientId, body),
  })
}
