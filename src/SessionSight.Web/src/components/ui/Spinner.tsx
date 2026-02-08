interface SpinnerProps {
  className?: string
}

export function Spinner({ className = '' }: SpinnerProps) {
  return (
    <div role="status" className={`flex items-center justify-center p-8 ${className}`}>
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-gray-200 border-t-blue-600" />
      <span className="sr-only">Loading...</span>
    </div>
  )
}
