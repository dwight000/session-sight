export function LegalDisclaimer() {
  return (
    <div className="border-t border-amber-200 bg-amber-50 px-3 py-3">
      <div className="flex items-start gap-2">
        <svg
          className="mt-0.5 h-4 w-4 shrink-0 text-amber-600"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth={2}
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
          />
        </svg>
        <div>
          <p className="text-xs font-semibold text-amber-800">
            Demo Only — Not for Clinical Use
          </p>
          <p className="mt-1 text-xs leading-snug text-amber-700">
            This is a portfolio application. AI-generated content is not
            clinically validated and must not inform patient care decisions. No
            liability is accepted.
          </p>
        </div>
      </div>
    </div>
  )
}
