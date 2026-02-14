// P6-003: Deploy workflow verified
import { Routes, Route } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { Dashboard } from './pages/Dashboard'
import { ReviewQueue } from './pages/ReviewQueue'
import { SessionDetail } from './pages/SessionDetail'
import { Patients } from './pages/Patients'
import { PatientTimeline } from './pages/PatientTimeline'
import { Sessions } from './pages/Sessions'
import { Upload } from './pages/Upload'
import { Therapists } from './pages/Therapists'
import { ProcessingJobs } from './pages/ProcessingJobs'

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<Dashboard />} />
        <Route path="/patients" element={<Patients />} />
        <Route path="/patients/:patientId/timeline" element={<PatientTimeline />} />
        <Route path="/sessions" element={<Sessions />} />
        <Route path="/therapists" element={<Therapists />} />
        <Route path="/jobs" element={<ProcessingJobs />} />
        <Route path="/upload" element={<Upload />} />
        <Route path="/review" element={<ReviewQueue />} />
        <Route path="/review/session/:sessionId" element={<SessionDetail />} />
      </Route>
    </Routes>
  )
}
