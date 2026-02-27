import { useState } from 'react'
import type { ClinicalExtraction, ExtractedField } from '../../types'
import { isExtractedField } from '../../utils/format'
import { formatFieldName } from '../../utils/format'

const SECTION_ORDER = [
  'sessionInfo',
  'presentingConcerns',
  'moodAssessment',
  'riskAssessment',
  'mentalStatusExam',
  'interventions',
  'diagnoses',
  'treatmentProgress',
  'nextSteps',
] as const

function confidenceColor(c: number): string {
  if (c < 0.5) return 'bg-red-100 text-red-800 border-red-200'
  if (c <= 0.8) return 'bg-yellow-100 text-yellow-800 border-yellow-200'
  return 'bg-green-100 text-green-800 border-green-200'
}

interface FieldEntry {
  sectionName: string
  fieldName: string
  field: ExtractedField
}

function flattenFields(data: ClinicalExtraction): FieldEntry[] {
  const entries: FieldEntry[] = []
  for (const section of SECTION_ORDER) {
    const sectionData = data[section]
    if (!sectionData || typeof sectionData !== 'object') continue
    for (const [key, val] of Object.entries(sectionData)) {
      if (isExtractedField(val)) {
        entries.push({ sectionName: section, fieldName: key, field: val })
      }
    }
  }
  return entries
}

interface ConfidenceHeatmapProps {
  data: ClinicalExtraction | undefined
  defaultOpen?: boolean
}

export function ConfidenceHeatmap({ data, defaultOpen = false }: ConfidenceHeatmapProps) {
  const [open, setOpen] = useState(defaultOpen)
  const [expandedKey, setExpandedKey] = useState<string | null>(null)

  if (!data) return null

  const fields = flattenFields(data)
  if (fields.length === 0) return null

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
        Field Confidence ({fields.length} fields)
      </button>
      {open && (
        <div className="mt-2 flex flex-wrap gap-1">
          {fields.map((entry) => {
            const key = `${entry.sectionName}.${entry.fieldName}`
            const isExpanded = expandedKey === key
            return (
              <div key={key}>
                <button
                  onClick={() => setExpandedKey(isExpanded ? null : key)}
                  className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs cursor-pointer ${confidenceColor(entry.field.confidence)}`}
                  title={`${formatFieldName(entry.fieldName)}: ${Math.round(entry.field.confidence * 100)}%`}
                >
                  {formatFieldName(entry.fieldName)}
                  <span className="ml-1 font-mono">{Math.round(entry.field.confidence * 100)}%</span>
                </button>
                {isExpanded && (
                  <div className="mt-1 rounded border border-gray-200 bg-gray-50 p-2 text-xs">
                    <p><span className="font-medium text-gray-500">Field:</span> {formatFieldName(entry.fieldName)}</p>
                    <p><span className="font-medium text-gray-500">Confidence:</span> {Math.round(entry.field.confidence * 100)}%</p>
                    {entry.field.source?.section && (
                      <p><span className="font-medium text-gray-500">Section:</span> {entry.field.source.section}</p>
                    )}
                    {entry.field.source?.text && (
                      <p className="mt-1 break-words"><span className="font-medium text-gray-500">Source:</span> {entry.field.source.text}</p>
                    )}
                    {entry.field.source && (
                      <p><span className="font-medium text-gray-500">Chars:</span> {entry.field.source.startChar}–{entry.field.source.endChar}</p>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
