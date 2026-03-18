import { useAuth } from '@clerk/react'
import { useState, useEffect } from 'react'
import { getMileage, createMileage } from '../services/api'

const CATEGORIES = ['Consulting', 'Client Visit', 'Office', 'Training', 'Other']

export default function Mileage() {
  const { getToken } = useAuth()
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showForm, setShowForm] = useState(false)

  const emptyForm = {
    tripDate: new Date().toISOString().slice(0, 10),
    startLocation: '', endLocation: '', miles: '',
    isReturn: false, purpose: '', category: 'Consulting', notes: ''
  }
  const [form, setForm] = useState(emptyForm)

  const loadTrips = async () => {
    try {
      setLoading(true)
      const data = await getMileage(getToken)
      setTrips(data || [])
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadTrips() }, [getToken])

  const handleSubmit = async (e) => {
    e.preventDefault()
    try {
      await createMileage({
        ...form,
        miles: parseFloat(form.miles) || 0,
        tripDate: new Date(form.tripDate).toISOString(),
      }, getToken)
      setShowForm(false)
      setForm(emptyForm)
      loadTrips()
    } catch (err) {
      setError(err.message)
    }
  }

  const milesDisplay = (trip) => {
    const m = trip.miles || 0
    return trip.isReturn ? `${m} (return)` : m
  }

  const statusBadge = (status) => {
    const colors = {
      Submitted: '#f59e0b', Approved: '#10b981', Rejected: '#ef4444', Draft: '#6b7280'
    }
    return <span className="status-badge" style={{ background: colors[status] || '#6b7280' }}>{status}</span>
  }

  if (loading) return <div className="loading">Loading mileage...</div>

  return (
    <div className="page">
      <div className="page-header">
        <h1>My Mileage</h1>
        <button className="btn btn-primary" onClick={() => { setShowForm(true); setForm(emptyForm) }}>
          🚗 Log Trip
        </button>
      </div>

      {error && <div className="error-banner">⚠️ {error} <button onClick={() => setError(null)}>✕</button></div>}

      {showForm && (
        <div className="modal-overlay" onClick={() => setShowForm(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>Log Mileage Trip</h2>
            <form onSubmit={handleSubmit}>
              <div className="form-grid">
                <label>
                  Date
                  <input type="date" value={form.tripDate} onChange={e => setForm(f => ({...f, tripDate: e.target.value}))} required />
                </label>
                <label>
                  Category
                  <select value={form.category} onChange={e => setForm(f => ({...f, category: e.target.value}))}>
                    {CATEGORIES.map(c => <option key={c}>{c}</option>)}
                  </select>
                </label>
                <label>
                  From
                  <input value={form.startLocation} onChange={e => setForm(f => ({...f, startLocation: e.target.value}))} placeholder="e.g. Home" required />
                </label>
                <label>
                  To
                  <input value={form.endLocation} onChange={e => setForm(f => ({...f, endLocation: e.target.value}))} placeholder="e.g. Client Office" required />
                </label>
                <label>
                  Miles
                  <input type="number" step="0.1" value={form.miles} onChange={e => setForm(f => ({...f, miles: e.target.value}))} required />
                </label>
                <label className="checkbox-label">
                  <input type="checkbox" checked={form.isReturn} onChange={e => setForm(f => ({...f, isReturn: e.target.checked}))} />
                  Return trip (miles includes both ways)
                </label>
              </div>
              <label>
                Purpose / Business Reason
                <input value={form.purpose} onChange={e => setForm(f => ({...f, purpose: e.target.value}))} placeholder="e.g. Client meeting with Acme Ltd" required />
              </label>
              <label>
                Notes (optional)
                <textarea value={form.notes} onChange={e => setForm(f => ({...f, notes: e.target.value}))} rows={2} />
              </label>
              <div className="form-actions">
                <button type="button" className="btn btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Submit Trip</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="card-list">
        {trips.length === 0 && <p className="empty">No mileage trips logged yet.</p>}
        {trips.map(trip => (
          <div key={trip.id} className="expense-card">
            <div className="card-top">
              <div>
                <strong>{trip.startLocation} → {trip.endLocation}</strong>
                <span className="card-date">{trip.tripDate ? new Date(trip.tripDate).toLocaleDateString('en-GB') : ''}</span>
              </div>
              <div className="card-amount">{milesDisplay(trip)} mi</div>
            </div>
            <div className="card-bottom">
              <span className="card-category">{trip.purpose}</span>
              {statusBadge(trip.approvalStatus)}
              <span className="card-amount-small">£{(trip.totalAmount || 0).toFixed(2)}</span>
            </div>
            {trip.approvalStatus === 'Rejected' && trip.rejectionReason && (
              <div className="rejection-reason">❌ {trip.rejectionReason}</div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
