import { useState } from 'react'

interface DocumentPreviewProps {
  sessionId: string
  defaultOpen?: boolean
}

export function DocumentPreview({ sessionId, defaultOpen = false }: DocumentPreviewProps) {
  const [open, setOpen] = useState(defaultOpen)
  const url = `/api/sessions/${sessionId}/document/download`

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        Document Preview
        <span className="ml-auto text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <iframe
          src={url}
          title="Document preview"
          className="mt-2 w-full rounded border border-gray-200"
          style={{ height: '600px' }}
        />
      )}
    </div>
  )
}
