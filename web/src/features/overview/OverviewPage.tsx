import { useAuth } from '../../auth/useAuth'
import { AdminOverview } from './AdminOverview'
import { CandidateOverview } from './CandidateOverview'
import { ReviewerOverview } from './ReviewerOverview'
import './overview.css'

/** Genel bakış: her rolün girişte gördüğü panel (kullanıcı isteği, K-42). */
export function OverviewPage() {
  const { user } = useAuth()
  if (!user) return null
  const firstName = (user.name ?? '').trim().split(/\s+/)[0] ?? ''
  if (user.role === 'Admin') return <AdminOverview firstName={firstName} />
  if (user.role === 'Reviewer') return <ReviewerOverview firstName={firstName} />
  return <CandidateOverview firstName={firstName} />
}
