import { useState } from 'react'
import { NavLink } from 'react-router-dom'

const links = [
  { to: '/', label: 'Dashboard' },
  { to: '/patients', label: 'Patients' },
  { to: '/sessions', label: 'Sessions' },
  { to: '/therapists', label: 'Therapists' },
  { to: '/jobs', label: 'Jobs' },
  { to: '/upload', label: 'Upload' },
  { to: '/review', label: 'Review Queue' },
]

export function MobileNav() {
  const [open, setOpen] = useState(false)

  return (
    <div className="border-b border-gray-200 bg-white md:hidden">
      <div className="flex items-center justify-between px-4 py-3">
        <h1 className="text-lg font-bold text-gray-900">SessionSight</h1>
        <button
          onClick={() => setOpen(!open)}
          className="rounded-md p-2 text-gray-500 hover:bg-gray-100 hover:text-gray-700"
        >
          <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            {open ? (
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            ) : (
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
            )}
          </svg>
        </button>
      </div>
      {open && (
        <nav className="space-y-1 px-2 pb-3">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.to === '/'}
              onClick={() => setOpen(false)}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 text-sm font-medium ${
                  isActive
                    ? 'bg-blue-50 text-blue-700'
                    : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
                }`
              }
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
      )}
    </div>
  )
}
