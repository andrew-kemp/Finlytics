import { useAuth } from '@clerk/react'
import { useState, useEffect, useRef } from 'react'
import { getExpenses, createExpense, updateExpense, deleteExpense, uploadReceipt, analyzeReceipt } from '../services/api'

const CATEGORIES = [
  'Travel', 'Fuel', 'Meals & Entertainment', 'Office Supplies',
  'Software & Subscriptions', 'Equipment', 'Parking', 'Accommodation',
  'Training', 'Phone & Internet', 'Professional Services', 'Other'
]

const VAT_RATES = [
  { label: 'Standard (20%)', value: 20 },
  { label: 'Reduced (5%)', value: 5 },
  { label: 'Zero (0%)', value: 0 },
  { label: 'Exempt', value: null },
]

export default function Expenses() {
  const { getToken } = useAuth()
  const [expenses, setExpenses] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showForm, setShowForm] = useState(false)
  const [editingExpense, setEditingExpense] = useState(null)
  const [scanning, setScanning] = useState(false)
  const fileInputRef = useRef(null)
  const [selectedFile, setSelectedFile] = useState(null)

  const emptyForm = {
    supplier: '', category: '', amountNet: '', vatAmount: '', amountGross: '',
    vatRate: 20, vatIncluded: true, vatApplicability: 'Standard',
    entryDate: new Date().toISOString().slice(0, 10), reference: '', notes: '',
    paymentMethod: 'Personal Card'
  }
  const [form, setForm] = useState(emptyForm)

  const loadExpenses = async () => {
    try {
      setLoading(true)
      const data = await getExpenses(getToken)
      setExpenses(data || [])
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadExpenses() }, [getToken])

  const handleScan = async (file) => {
    setScanning(true)
    setSelectedFile(file)
    try {
      const result = await analyzeReceipt(file, getToken)
      if (result && !result.notConfigured) {
        setForm(f => ({
          ...f,
          supplier: result.vendor || f.supplier,
          reference: result.invoiceRef || f.reference,
          entryDate: result.invoiceDate ? result.invoiceDate.slice(0, 10) : f.entryDate,
          amountNet: result.subtotal || result.total || f.amountNet,
          vatAmount: result.vatAmount || f.vatAmount,
          amountGross: result.total || f.amountGross,
        }))
      }
    } catch { /* OCR not available, that's fine */ }
    setScanning(false)
  }

  const recalcVat = (field, value) => {
    const f = { ...form, [field]: value }
    const rate = parseFloat(f.vatRate) || 0
    if (field === 'amountGross' && f.vatIncluded && rate > 0) {
      const gross = parseFloat(value) || 0
      const net = gross / (1 + rate / 100)
      f.amountNet = net.toFixed(2)
      f.vatAmount = (gross - net).toFixed(2)
    } else if (field === 'amountNet' && !f.vatIncluded && rate > 0) {
      const net = parseFloat(value) || 0
      f.vatAmount = (net * rate / 100).toFixed(2)
      f.amountGross = (net + parseFloat(f.vatAmount)).toFixed(2)
    }
    setForm(f)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    try {
      const payload = {
        ...form,
        amountNet: parseFloat(form.amountNet) || 0,
        vatAmount: parseFloat(form.vatAmount) || 0,
        amountGross: parseFloat(form.amountGross) || 0,
        vatRate: parseFloat(form.vatRate) || 0,
        entryDate: form.entryDate ? new Date(form.entryDate).toISOString() : null,
      }

      let saved
      if (editingExpense) {
        saved = await updateExpense(editingExpense.id, payload, getToken)
      } else {
        saved = await createExpense(payload, getToken)
      }

      // Upload receipt if file selected
      if (selectedFile && saved?.id) {
        await uploadReceipt(saved.id, selectedFile, getToken)
      }

      setShowForm(false)
      setForm(emptyForm)
      setEditingExpense(null)
      setSelectedFile(null)
      loadExpenses()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleEdit = (exp) => {
    setEditingExpense(exp)
    setForm({
      supplier: exp.supplier || '',
      category: exp.category || '',
      amountNet: exp.amountNet || '',
      vatAmount: exp.vatAmount || '',
      amountGross: exp.amountGross || '',
      vatRate: 20,
      vatIncluded: true,
      vatApplicability: 'Standard',
      entryDate: exp.entryDate ? new Date(exp.entryDate).toISOString().slice(0, 10) : '',
      reference: exp.reference || '',
      notes: exp.notes || '',
      paymentMethod: exp.paymentMethod || 'Personal Card',
    })
    setShowForm(true)
  }

  const handleDelete = async (id) => {
    if (!confirm('Delete this draft expense?')) return
    try {
      await deleteExpense(id, getToken)
      loadExpenses()
    } catch (err) {
      setError(err.message)
    }
  }

  const statusBadge = (status) => {
    const colors = {
      Submitted: '#f59e0b', Approved: '#10b981', Rejected: '#ef4444', Draft: '#6b7280', NotRequired: '#8b5cf6'
    }
    return (
      <span className="status-badge" style={{ background: colors[status] || '#6b7280' }}>
        {status}
      </span>
    )
  }

  if (loading) return <div className="loading">Loading expenses...</div>

  return (
    <div className="page">
      <div className="page-header">
        <h1>My Expenses</h1>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={() => fileInputRef.current?.click()}>
            📸 Scan Receipt
          </button>
          <button className="btn btn-primary" onClick={() => { setShowForm(true); setEditingExpense(null); setForm(emptyForm); setSelectedFile(null) }}>
            + New Expense
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*,application/pdf"
            capture="environment"
            style={{ display: 'none' }}
            onChange={(e) => {
              if (e.target.files[0]) {
                setShowForm(true)
                setEditingExpense(null)
                setForm(emptyForm)
                handleScan(e.target.files[0])
              }
            }}
          />
        </div>
      </div>

      {error && <div className="error-banner">⚠️ {error} <button onClick={() => setError(null)}>✕</button></div>}

      {showForm && (
        <div className="modal-overlay" onClick={() => setShowForm(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>{editingExpense ? 'Edit Expense' : 'New Expense'}</h2>
            {scanning && <div className="scanning">🔍 Scanning receipt...</div>}
            <form onSubmit={handleSubmit}>
              <div className="form-grid">
                <label>
                  Supplier
                  <input value={form.supplier} onChange={e => setForm(f => ({...f, supplier: e.target.value}))} required />
                </label>
                <label>
                  Category
                  <select value={form.category} onChange={e => setForm(f => ({...f, category: e.target.value}))} required>
                    <option value="">Select...</option>
                    {CATEGORIES.map(c => <option key={c}>{c}</option>)}
                  </select>
                </label>
                <label>
                  Date
                  <input type="date" value={form.entryDate} onChange={e => setForm(f => ({...f, entryDate: e.target.value}))} required />
                </label>
                <label>
                  Reference
                  <input value={form.reference} onChange={e => setForm(f => ({...f, reference: e.target.value}))} placeholder="Invoice # / ref" />
                </label>
                <label>
                  Amount (gross)
                  <input type="number" step="0.01" value={form.amountGross} onChange={e => recalcVat('amountGross', e.target.value)} required />
                </label>
                <label>
                  VAT
                  <input type="number" step="0.01" value={form.vatAmount} onChange={e => setForm(f => ({...f, vatAmount: e.target.value}))} />
                </label>
                <label>
                  Net
                  <input type="number" step="0.01" value={form.amountNet} onChange={e => recalcVat('amountNet', e.target.value)} />
                </label>
                <label>
                  Payment Method
                  <select value={form.paymentMethod} onChange={e => setForm(f => ({...f, paymentMethod: e.target.value}))}>
                    <option>Personal Card</option>
                    <option>Company Card</option>
                    <option>Cash</option>
                    <option>Bank Transfer</option>
                  </select>
                </label>
              </div>
              <label>
                Notes
                <textarea value={form.notes} onChange={e => setForm(f => ({...f, notes: e.target.value}))} rows={2} />
              </label>

              {!editingExpense && (
                <label className="file-label">
                  📎 Attach Receipt
                  <input type="file" accept="image/*,application/pdf" onChange={e => setSelectedFile(e.target.files[0])} />
                  {selectedFile && <span className="file-name">{selectedFile.name}</span>}
                </label>
              )}

              <div className="form-actions">
                <button type="button" className="btn btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">
                  {editingExpense ? 'Resubmit' : 'Submit Expense'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="card-list">
        {expenses.length === 0 && <p className="empty">No expenses yet. Tap "New Expense" or scan a receipt to get started.</p>}
        {expenses.map(exp => (
          <div key={exp.id} className="expense-card">
            <div className="card-top">
              <div>
                <strong>{exp.supplier || 'No supplier'}</strong>
                <span className="card-date">{exp.entryDate ? new Date(exp.entryDate).toLocaleDateString('en-GB') : ''}</span>
              </div>
              <div className="card-amount">£{(exp.amountGross || 0).toFixed(2)}</div>
            </div>
            <div className="card-bottom">
              <span className="card-category">{exp.category}</span>
              {statusBadge(exp.approvalStatus)}
              {exp.hasReceipt && <span className="receipt-badge">📎</span>}
            </div>
            {exp.approvalStatus === 'Rejected' && exp.rejectionReason && (
              <div className="rejection-reason">❌ {exp.rejectionReason}</div>
            )}
            <div className="card-actions">
              {(exp.approvalStatus === 'Draft' || exp.approvalStatus === 'Rejected') && (
                <>
                  <button className="btn-sm" onClick={() => handleEdit(exp)}>Edit</button>
                  {exp.approvalStatus === 'Draft' && (
                    <button className="btn-sm btn-danger" onClick={() => handleDelete(exp.id)}>Delete</button>
                  )}
                </>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
