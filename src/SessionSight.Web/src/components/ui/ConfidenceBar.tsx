interface ConfidenceBarProps {
  value: number
  className?: string
}

function getConfidenceColor(value: number): string {
  if (value < 0.7) return 'bg-red-500'
  if (value < 0.9) return 'bg-yellow-500'
  return 'bg-green-500'
}

export function ConfidenceBar({ value, className = '' }: ConfidenceBarProps) {
  const pct = Math.round(value * 100)
  const color = getConfidenceColor(value)

  return (
    <div className={`flex items-center gap-2 ${className}`}>
      <div className="h-2 w-20 rounded-full bg-gray-200">
        <div className={`h-2 rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs text-gray-500">{pct}%</span>
    </div>
  )
}
