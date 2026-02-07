interface ConfidenceBarProps {
  value: number
  className?: string
}

export function ConfidenceBar({ value, className = '' }: ConfidenceBarProps) {
  const pct = Math.round(value * 100)
  const color = value < 0.7 ? 'bg-red-500' : value < 0.9 ? 'bg-yellow-500' : 'bg-green-500'

  return (
    <div className={`flex items-center gap-2 ${className}`}>
      <div className="h-2 w-20 rounded-full bg-gray-200">
        <div className={`h-2 rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs text-gray-500">{pct}%</span>
    </div>
  )
}
