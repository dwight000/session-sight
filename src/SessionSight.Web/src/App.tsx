import { Routes, Route } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { Dashboard } from './pages/Dashboard'
import { ReviewQueue } from './pages/ReviewQueue'
import { SessionDetail } from './pages/SessionDetail'
import { Patients } from './pages/Patients'
import { Sessions } from './pages/Sessions'
import { Upload } from './pages/Upload'

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<Dashboard />} />
        <Route path="/patients" element={<Patients />} />
        <Route path="/sessions" element={<Sessions />} />
        <Route path="/upload" element={<Upload />} />
        <Route path="/review" element={<ReviewQueue />} />
        <Route path="/review/session/:sessionId" element={<SessionDetail />} />
      </Route>
    </Routes>
  )
}
