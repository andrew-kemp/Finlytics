import { useAuth } from '@clerk/react'
import { useState, useEffect } from 'react'
import { getProfile, getExpenses, getMileage, getMileageTracker } from '../services/api'

export default function Dashboard() {
  const { getToken } = useAuth()
  const [profile, setProfile] = useState(null)
  const [stats, setStats] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    async function load() {
      try {
        const [prof, expensesRaw, mileageRaw, tracker] = await Promise.all([
          getProfile(getToken),
          getExpenses(getToken).catch(() => []),
          getMileage(getToken).catch(() => []),
          getMileageTracker(getToken).catch(() => null),
        ])
        const expenses = Array.isArray(expensesRaw) ? expensesRaw : []
        const mileage = Array.isArray(mileageRaw) ? mileageRaw : []
        setProfile(prof)
        setStats({
          totalExpenses: expenses.length,
          pendingExpenses: expenses.filter(e => e.approvalStatus === 'Submitted').length,
          approvedExpenses: expenses.filter(e => e.approvalStatus === 'Approved').length,
          rejectedExpenses: expenses.filter(e => e.approvalStatus === 'Rejected').length,
          totalMileageTrips: mileage.length,
          mileageTracker: tracker,
        })
      } catch (err) {
        setError(err.message)
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [getToken])

  if (loading) return <div className="loading">Loading dashboard...</div>
  if (error) return <div className="error-banner">⚠️ {error}</div>

  const tracker = stats?.mileageTracker
  const pct = tracker?.percentUsed || 0

  return (
    <div className="dashboard">
      <h1>Welcome, {profile?.displayName || profile?.email || 'there'} 👋</h1>

      <div className="stat-grid">
        <div className="stat-card">
          <div className="stat-number">{stats.totalExpenses}</div>
          <div className="stat-label">Total Expenses</div>
        </div>
        <div className="stat-card pending">
          <div className="stat-number">{stats.pendingExpenses}</div>
          <div className="stat-label">Pending Approval</div>
        </div>
        <div className="stat-card approved">
          <div className="stat-number">{stats.approvedExpenses}</div>
          <div className="stat-label">Approved</div>
        </div>
        <div className="stat-card rejected">
          <div className="stat-number">{stats.rejectedExpenses}</div>
          <div className="stat-label">Rejected</div>
        </div>
      </div>

      {tracker && (
        <div className="tracker-card">
          <h2>🚗 Mileage Allowance {tracker.taxYear}</h2>
          <div className="tracker-bar-container">
            <div className="tracker-bar" style={{ width: `${Math.min(pct, 100)}%` }} />
          </div>
          <div className="tracker-stats">
            <span>{tracker.approvedMiles?.toLocaleString()} of {tracker.threshold?.toLocaleString()} miles</span>
            <span className="tracker-pct">{pct}%</span>
          </div>
          <div className="tracker-details">
            <div>
              <strong>At 45p:</strong> {tracker.milesAt45p?.toLocaleString()} miles = £{tracker.amountClaimed?.toFixed(2)}
            </div>
            <div>
              <strong>Remaining at 45p:</strong> {tracker.remainingMilesAt45p?.toLocaleString()} miles (£{tracker.remainingValueAt45p?.toFixed(2)})
            </div>
            {tracker.isOver10k && (
              <div className="rate-warning">⚠️ Over 10,000 miles — rate now 25p/mile</div>
            )}
          </div>
        </div>
      )}

      <div className="stat-grid" style={{ marginTop: '1rem' }}>
        <div className="stat-card">
          <div className="stat-number">{stats.totalMileageTrips}</div>
          <div className="stat-label">Mileage Trips</div>
        </div>
        <div className="stat-card">
          <div className="stat-number">{tracker?.totalMilesLogged?.toLocaleString() || 0}</div>
          <div className="stat-label">Miles Logged</div>
        </div>
      </div>
    </div>
  )
}
