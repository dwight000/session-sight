import { Badge } from './Badge'

const riskVariants: Record<string, string> = {
  low: 'success',
  moderate: 'warning',
  high: 'danger',
  imminent: 'purple',
}

interface RiskBadgeProps {
  level: string
}

export function RiskBadge({ level }: RiskBadgeProps) {
  const normalized = level.toLowerCase()
  const variant = riskVariants[normalized] || 'default'
  return <Badge variant={variant}>{level}</Badge>
}
