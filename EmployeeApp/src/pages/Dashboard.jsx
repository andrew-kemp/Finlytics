

import { useAuth } from '@clerk/react'
import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { getProfile, getExpenses, getMileage, getMileageTracker } from '../services/api'

const StatCard = ({ label, value, icon, className = '' }) => (
  <div className={`stat-card ${className}`}>
    <div className="stat-icon">{icon}</div>
    <div className="stat-info">
      <div className="stat-value">{value}</div>
      <div className="stat-label">{label}</div>
    </div>
  </div>
);

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
      <header className="dashboard-header">
        <h1>Welcome, {profile?.displayName || profile?.email || 'there'} 👋</h1>
        <p>Here's a summary of your expenses and mileage activity.</p>
      </header>

      <div className="quick-actions">
        <Link to="/expenses?new=true" className="btn-primary">＋ New Expense</Link>
        <Link to="/mileage?new=true" className="btn-secondary">＋ Add Mileage</Link>
      </div>

      <div className="dashboard-grid">
        <div className="main-content">
          <h2 className="section-title">💰 Expenses</h2>
          <div className="stat-grid">
            <StatCard label="Total Expenses" value={stats.totalExpenses} icon="🧾" />
            <StatCard label="Pending Approval" value={stats.pendingExpenses} icon="⏳" className="pending" />
            <StatCard label="Approved" value={stats.approvedExpenses} icon="✅" className="approved" />
            <StatCard label="Rejected" value={stats.rejectedExpenses} icon="❌" className="rejected" />
          </div>

          <h2 className="section-title">🚗 Mileage</h2>
          <div className="stat-grid">
            <StatCard label="Mileage Trips" value={stats.totalMileageTrips} icon="🗺️" />
            <StatCard label="Miles Logged" value={tracker?.totalMilesLogged?.toLocaleString() || 0} icon="📏" />
          </div>
        </div>

        <aside className="sidebar-content">
          {tracker && (
            <div className="tracker-card">
              <h2 className="section-title">Mileage Allowance {tracker.taxYear}</h2>
              <div className="tracker-bar-container">
                <div className="tracker-bar" style={{ width: `${Math.min(pct, 100)}%` }} />
              </div>
              <div className="tracker-stats">
                <span>{tracker.approvedMiles?.toLocaleString()} / {tracker.threshold?.toLocaleString()} mi</span>
                <span className="tracker-pct">{pct}%</span>
              </div>
              <div className="tracker-details">
                <div className="detail-item">
                  <span>At 45p ({tracker.milesAt45p?.toLocaleString()} mi)</span>
                  <strong>£{tracker.amountClaimed?.toFixed(2)}</strong>
                </div>
                <div className="detail-item">
                  <span>Remaining at 45p</span>
                  <strong>{tracker.remainingMilesAt45p?.toLocaleString()} mi (£{tracker.remainingValueAt45p?.toFixed(2)})</strong>
                </div>
                {tracker.isOver10k && (
                  <div className="rate-warning">⚠️ Over 10,000 miles — rate now 25p/mile</div>
                )}
              </div>
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}
