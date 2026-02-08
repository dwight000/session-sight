import type { PatientRiskTrendPoint } from '../../types/riskTrend'

interface RiskTrendChartProps {
  points: PatientRiskTrendPoint[]
}

const LEVELS = [
  { label: 'Imminent', value: 3 },
  { label: 'High', value: 2 },
  { label: 'Moderate', value: 1 },
  { label: 'Low', value: 0 },
]

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

export function RiskTrendChart({ points }: RiskTrendChartProps) {
  if (points.length === 0) {
    return <p className="text-sm text-gray-500">No sessions in this date range.</p>
  }

  const validPoints = points.filter((point) => point.riskScore !== null)
  if (validPoints.length === 0) {
    return <p className="text-sm text-gray-500">No extracted risk levels available for this period.</p>
  }

  const width = 720
  const height = 260
  const leftPad = 80
  const rightPad = 20
  const topPad = 20
  const bottomPad = 44
  const plotWidth = width - leftPad - rightPad
  const plotHeight = height - topPad - bottomPad
  const stepX = points.length > 1 ? plotWidth / (points.length - 1) : 0

  const xAt = (index: number) => leftPad + index * stepX
  const yAt = (riskScore: number) => topPad + ((3 - riskScore) / 3) * plotHeight

  const linePath = validPoints
    .map((point) => {
      const index = points.findIndex((p) => p.sessionId === point.sessionId)
      return `${xAt(index)},${yAt(point.riskScore as number)}`
    })
    .join(' ')

  return (
    <div className="space-y-3">
      {/* NOSONAR: SVG chart cannot use <img>, role="img" with aria-label is the accessible pattern */}
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Patient risk trend chart" className="w-full">
        {LEVELS.map((level) => (
          <g key={level.label}>
            <line
              x1={leftPad}
              y1={yAt(level.value)}
              x2={width - rightPad}
              y2={yAt(level.value)}
              stroke="#e5e7eb"
              strokeWidth="1"
            />
            <text x={leftPad - 10} y={yAt(level.value) + 4} textAnchor="end" className="fill-gray-600 text-[11px]">
              {level.label}
            </text>
          </g>
        ))}

        <polyline fill="none" stroke="#2563eb" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" points={linePath} />

        {points.map((point, index) => {
          if (point.riskScore === null) return null
          return (
            <circle key={point.sessionId} cx={xAt(index)} cy={yAt(point.riskScore)} r="4.5" fill="#2563eb">
              <title>{`Session ${point.sessionNumber} (${formatDate(point.sessionDate)}): ${point.riskLevel}`}</title>
            </circle>
          )
        })}
      </svg>

      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-gray-600">
        <span>{`Start: ${formatDate(points[0].sessionDate)}`}</span>
        <span>{`End: ${formatDate(points[points.length - 1].sessionDate)}`}</span>
      </div>
    </div>
  )
}
