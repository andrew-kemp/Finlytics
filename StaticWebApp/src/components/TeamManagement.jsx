import React, { useState, useEffect } from 'react';
import { getTeamMembers, updateTeamMember, deleteTeamMember, getTeamApprovals, approveItem, rejectItem } from '../services/apiService';
import Toast from './Toast';
import { useToast } from '../hooks/useToast';

const STATUS_STYLES = {
  Invited:  { bg: '#fef9c3', color: '#854d0e', icon: '📧' },
  Active:   { bg: '#dcfce7', color: '#166534', icon: '✅' },
  Disabled: { bg: '#fee2e2', color: '#dc2626', icon: '🚫' },
};

const ROLE_STYLES = {
  Employee: { bg: '#e0e7ff', color: '#3730a3' },
  Admin:    { bg: '#dbeafe', color: '#1d4ed8' },
  Approver: { bg: '#fae8ff', color: '#86198f' },
};

export default function TeamManagement() {
  const [members, setMembers] = useState([]);
  const [approvals, setApprovals] = useState({ expenses: [], mileage: [] });
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(false);
  const [processingMessage, setProcessingMessage] = useState('');
  const [activeSection, setActiveSection] = useState('members'); // members | approvals
  const [viewingMember, setViewingMember] = useState(null);
  const [rejectingItem, setRejectingItem] = useState(null);
  const [rejectReason, setRejectReason] = useState('');
  const { toast, showToast, clearToast } = useToast();

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [membersData, approvalsData] = await Promise.all([
        getTeamMembers(),
        getTeamApprovals().catch(() => ({ expenses: [], mileage: [] }))
      ]);
      setMembers(membersData);
      setApprovals(approvalsData);
    } catch (error) {
      console.error('Error loading team data:', error);
      showToast('Failed to load team data', 'error');
    } finally {
      setLoading(false);
    }
  };

  const handleDisableMember = async (member) => {
    const action = member.status === 'Disabled' ? 'enable' : 'disable';
    if (!confirm(`Are you sure you want to ${action} ${member.displayName || member.email}?`)) return;

    setProcessingMessage(action === 'disable' ? 'Disabling member...' : 'Enabling member...');
    setProcessing(true);
    try {
      const newStatus = member.status === 'Disabled' ? 'Active' : 'Disabled';
      await updateTeamMember(member.id, { status: newStatus });
      showToast(`${member.displayName || member.email} ${action}d successfully`, 'success');
      await loadData();
      setViewingMember(null);
    } catch (error) {
      showToast(`Failed to ${action} member: ${error.message}`, 'error');
    } finally {
      setProcessing(false);
    }
  };

  const handleRemoveMember = async (member) => {
    if (!confirm(`Permanently remove ${member.displayName || member.email} from the team?\n\nThis will revoke their access to the expense portal.`)) return;

    setProcessingMessage('Removing member...');
    setProcessing(true);
    try {
      await deleteTeamMember(member.id);
      showToast(`${member.displayName || member.email} removed from team`, 'success');
      await loadData();
      setViewingMember(null);
    } catch (error) {
      showToast(`Failed to remove member: ${error.message}`, 'error');
    } finally {
      setProcessing(false);
    }
  };

  const handleApprove = async (type, id) => {
    setProcessingMessage('Approving...');
    setProcessing(true);
    try {
      await approveItem(type, id);
      showToast('Approved successfully!', 'success');
      await loadData();
    } catch (error) {
      showToast(`Failed to approve: ${error.message}`, 'error');
    } finally {
      setProcessing(false);
    }
  };

  const handleReject = async () => {
    if (!rejectingItem) return;
    setProcessingMessage('Rejecting...');
    setProcessing(true);
    try {
      await rejectItem(rejectingItem.type, rejectingItem.id, rejectReason);
      showToast('Rejected', 'success');
      setRejectingItem(null);
      setRejectReason('');
      await loadData();
    } catch (error) {
      showToast(`Failed to reject: ${error.message}`, 'error');
    } finally {
      setProcessing(false);
    }
  };

  const handleChangeRole = async (member, newRole) => {
    setProcessingMessage('Updating role...');
    setProcessing(true);
    try {
      await updateTeamMember(member.id, { role: newRole });
      showToast(`${member.displayName || member.email} role updated to ${newRole}`, 'success');
      await loadData();
    } catch (error) {
      showToast(`Failed to update role: ${error.message}`, 'error');
    } finally {
      setProcessing(false);
    }
  };

  const pendingCount = (approvals.expenses?.length || 0) + (approvals.mileage?.length || 0);

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <div className="loading-text">Loading team data...</div>
      </div>
    );
  }

  if (processing) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <div className="loading-text">{processingMessage || 'Please wait...'}</div>
      </div>
    );
  }

  return (
    <div className="employees-container">
      <Toast toast={toast} onClose={clearToast} />

      <div className="page-header">
        <h1>Team Management</h1>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            className={activeSection === 'members' ? 'btn-primary' : 'btn-secondary'}
            onClick={() => setActiveSection('members')}>
            👥 Members ({members.length})
          </button>
          <button
            className={activeSection === 'approvals' ? 'btn-primary' : 'btn-secondary'}
            onClick={() => setActiveSection('approvals')}
            style={pendingCount > 0 ? { position: 'relative' } : {}}>
            ✅ Approvals
            {pendingCount > 0 && (
              <span style={{
                position: 'absolute', top: '-6px', right: '-6px',
                background: '#dc2626', color: 'white', borderRadius: '50%',
                width: '20px', height: '20px', fontSize: '0.7em',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontWeight: 700
              }}>
                {pendingCount}
              </span>
            )}
          </button>
        </div>
      </div>

      {/* ── Members Section ── */}
      {activeSection === 'members' && (
        <div className="employees-list">
          {members.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '3rem 1rem', color: '#6b7280' }}>
              <div style={{ fontSize: '2.5rem', marginBottom: '0.75rem' }}>👥</div>
              <p style={{ fontSize: '1.05em', fontWeight: 500 }}>No team members yet</p>
              <p style={{ fontSize: '0.88em' }}>
                Go to <strong>Employees</strong> → click an employee → <strong>📧 Invite to Expenses</strong> to get started.
              </p>
            </div>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th style={{ minWidth: '150px' }}>Name</th>
                  <th style={{ minWidth: '200px' }}>Email</th>
                  <th style={{ minWidth: '90px' }}>Role</th>
                  <th style={{ minWidth: '90px' }}>Status</th>
                  <th style={{ minWidth: '120px' }}>Invited</th>
                  <th style={{ minWidth: '100px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {members.map(member => {
                  const ss = STATUS_STYLES[member.status] || STATUS_STYLES.Invited;
                  const rs = ROLE_STYLES[member.role] || ROLE_STYLES.Employee;
                  return (
                    <tr key={member.id} style={{ cursor: 'pointer' }}
                        onClick={() => setViewingMember(member)}>
                      <td><strong>{member.displayName || '—'}</strong></td>
                      <td>{member.email}</td>
                      <td>
                        <span style={{
                          background: rs.bg, color: rs.color, borderRadius: '0.25rem',
                          padding: '0.1rem 0.5rem', fontSize: '0.82em', fontWeight: 500
                        }}>
                          {member.role}
                        </span>
                      </td>
                      <td>
                        <span style={{
                          background: ss.bg, color: ss.color, borderRadius: '0.25rem',
                          padding: '0.1rem 0.5rem', fontSize: '0.82em', fontWeight: 500
                        }}>
                          {ss.icon} {member.status}
                        </span>
                      </td>
                      <td style={{ fontSize: '0.85em', color: '#6b7280' }}>
                        {member.invitedAt ? new Date(member.invitedAt).toLocaleDateString('en-GB') : '—'}
                      </td>
                      <td onClick={e => e.stopPropagation()}>
                        {member.status !== 'Disabled' ? (
                          <button onClick={() => handleDisableMember(member)}
                            className="btn-icon" title="Disable" style={{ color: '#dc2626' }}>
                            🔴
                          </button>
                        ) : (
                          <button onClick={() => handleDisableMember(member)}
                            className="btn-icon" title="Enable" style={{ color: '#16a34a' }}>
                            ✅
                          </button>
                        )}
                        <button onClick={() => handleRemoveMember(member)}
                          className="btn-icon" title="Remove" style={{ marginLeft: '5px', color: '#dc2626' }}>
                          🗑️
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ── Approvals Section ── */}
      {activeSection === 'approvals' && (
        <div>
          {pendingCount === 0 ? (
            <div style={{ textAlign: 'center', padding: '3rem 1rem', color: '#6b7280' }}>
              <div style={{ fontSize: '2.5rem', marginBottom: '0.75rem' }}>✅</div>
              <p style={{ fontSize: '1.05em', fontWeight: 500 }}>No pending approvals</p>
              <p style={{ fontSize: '0.88em' }}>When employees submit expenses or mileage claims, they'll appear here for approval.</p>
            </div>
          ) : (
            <>
              {/* Expense Approvals */}
              {approvals.expenses?.length > 0 && (
                <div style={{ marginBottom: '2rem' }}>
                  <h3 style={{ fontSize: '0.95em', color: '#374151', marginBottom: '0.75rem' }}>
                    💰 Expense Claims ({approvals.expenses.length})
                  </h3>
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Employee</th>
                        <th>Description</th>
                        <th>Amount</th>
                        <th>Date</th>
                        <th>Category</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {approvals.expenses.map(expense => (
                        <tr key={expense.id}>
                          <td><strong>{expense.employeeName || expense.submittedBy || '—'}</strong></td>
                          <td>{expense.description || '—'}</td>
                          <td>£{expense.amount?.toFixed(2) || '0.00'}</td>
                          <td style={{ fontSize: '0.85em' }}>
                            {expense.date ? new Date(expense.date).toLocaleDateString('en-GB') : '—'}
                          </td>
                          <td>{expense.category || '—'}</td>
                          <td>
                            <button className="btn-icon" title="Approve"
                              onClick={() => handleApprove('expense', expense.id)}
                              style={{ color: '#16a34a' }}>
                              ✅
                            </button>
                            <button className="btn-icon" title="Reject"
                              onClick={() => setRejectingItem({ type: 'expense', id: expense.id, name: expense.description })}
                              style={{ marginLeft: '5px', color: '#dc2626' }}>
                              ❌
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Mileage Approvals */}
              {approvals.mileage?.length > 0 && (
                <div>
                  <h3 style={{ fontSize: '0.95em', color: '#374151', marginBottom: '0.75rem' }}>
                    🚗 Mileage Claims ({approvals.mileage.length})
                  </h3>
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Employee</th>
                        <th>Trip</th>
                        <th>Miles</th>
                        <th>Amount</th>
                        <th>Date</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {approvals.mileage.map(trip => (
                        <tr key={trip.id}>
                          <td><strong>{trip.employeeName || trip.submittedBy || '—'}</strong></td>
                          <td>{trip.from} → {trip.to}</td>
                          <td>{trip.miles?.toFixed(1) || '0'}</td>
                          <td>£{trip.amount?.toFixed(2) || '0.00'}</td>
                          <td style={{ fontSize: '0.85em' }}>
                            {trip.date ? new Date(trip.date).toLocaleDateString('en-GB') : '—'}
                          </td>
                          <td>
                            <button className="btn-icon" title="Approve"
                              onClick={() => handleApprove('mileage', trip.id)}
                              style={{ color: '#16a34a' }}>
                              ✅
                            </button>
                            <button className="btn-icon" title="Reject"
                              onClick={() => setRejectingItem({ type: 'mileage', id: trip.id, name: `${trip.from} → ${trip.to}` })}
                              style={{ marginLeft: '5px', color: '#dc2626' }}>
                              ❌
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {/* ── View Member Modal ── */}
      {viewingMember && (
        <div className="modal-overlay" onClick={() => setViewingMember(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}
               style={{ maxWidth: '480px', width: '95%' }}>

            <div className="modal-header">
              <div>
                <h3 style={{ margin: 0 }}>{viewingMember.displayName || viewingMember.email}</h3>
                <div style={{ fontSize: '0.78em', color: '#888', marginTop: '0.15rem' }}>
                  {(() => {
                    const ss = STATUS_STYLES[viewingMember.status] || STATUS_STYLES.Invited;
                    return (
                      <span style={{ background: ss.bg, color: ss.color, borderRadius: '0.25rem',
                                     padding: '0.1rem 0.4rem', fontSize: '0.9em' }}>
                        {ss.icon} {viewingMember.status}
                      </span>
                    );
                  })()}
                </div>
              </div>
              <button className="btn-close" onClick={() => setViewingMember(null)}>✕</button>
            </div>

            <div className="modal-body" style={{ padding: '1.25rem' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem 1.5rem', fontSize: '0.88em' }}>
                <div>
                  <span style={{ color: '#9ca3af' }}>Email</span><br/>
                  <strong>{viewingMember.email}</strong>
                </div>
                <div>
                  <span style={{ color: '#9ca3af' }}>Role</span><br/>
                  <select value={viewingMember.role}
                    onChange={e => handleChangeRole(viewingMember, e.target.value)}
                    style={{ fontWeight: 600, padding: '0.25rem 0.5rem', borderRadius: '0.25rem',
                             border: '1px solid #d1d5db', cursor: 'pointer' }}>
                    <option value="Employee">Employee</option>
                    <option value="Admin">Admin</option>
                  </select>
                </div>
                <div>
                  <span style={{ color: '#9ca3af' }}>Invited</span><br/>
                  <strong>{viewingMember.invitedAt ? new Date(viewingMember.invitedAt).toLocaleDateString('en-GB') : '—'}</strong>
                </div>
                <div>
                  <span style={{ color: '#9ca3af' }}>Accepted</span><br/>
                  <strong>{viewingMember.acceptedAt ? new Date(viewingMember.acceptedAt).toLocaleDateString('en-GB') : 'Not yet'}</strong>
                </div>
                {viewingMember.employeeId && (
                  <div>
                    <span style={{ color: '#9ca3af' }}>Linked Employee</span><br/>
                    <strong>Employee #{viewingMember.employeeId}</strong>
                  </div>
                )}
                {viewingMember.clerkUserId && (
                  <div>
                    <span style={{ color: '#9ca3af' }}>Clerk User</span><br/>
                    <strong style={{ fontFamily: 'monospace', fontSize: '0.85em' }}>{viewingMember.clerkUserId}</strong>
                  </div>
                )}
              </div>
            </div>

            <div className="modal-footer" style={{ borderTop: '1px solid #e5e7eb',
                                                    padding: '0.875rem 1.25rem',
                                                    display: 'flex', justifyContent: 'space-between' }}>
              <button className="btn-secondary"
                onClick={() => handleDisableMember(viewingMember)}
                style={{ color: viewingMember.status === 'Disabled' ? '#16a34a' : '#dc2626',
                         borderColor: viewingMember.status === 'Disabled' ? '#86efac' : '#fca5a5' }}>
                {viewingMember.status === 'Disabled' ? '✅ Re-enable' : '🔴 Disable'}
              </button>
              <button className="btn-secondary"
                onClick={() => handleRemoveMember(viewingMember)}
                style={{ color: '#dc2626', borderColor: '#fca5a5' }}>
                🗑️ Remove
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Reject Reason Modal ── */}
      {rejectingItem && (
        <div className="modal-overlay" onClick={() => { setRejectingItem(null); setRejectReason(''); }}>
          <div className="modal-content" onClick={e => e.stopPropagation()}
               style={{ maxWidth: '420px', width: '95%' }}>
            <div className="modal-header">
              <h3 style={{ margin: 0 }}>Reject: {rejectingItem.name}</h3>
              <button className="btn-close" onClick={() => { setRejectingItem(null); setRejectReason(''); }}>✕</button>
            </div>
            <div className="modal-body" style={{ padding: '1.25rem' }}>
              <div className="form-group">
                <label>Reason for rejection</label>
                <textarea rows="3" value={rejectReason}
                  onChange={e => setRejectReason(e.target.value)}
                  placeholder="e.g., Missing receipt, incorrect amount..." autoFocus />
              </div>
            </div>
            <div className="modal-footer" style={{ borderTop: '1px solid #e5e7eb',
                                                    padding: '0.875rem 1.25rem',
                                                    display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button className="btn-secondary" onClick={() => { setRejectingItem(null); setRejectReason(''); }}>
                Cancel
              </button>
              <button className="btn-primary" onClick={handleReject}
                style={{ background: '#dc2626', borderColor: '#dc2626' }}>
                ❌ Reject
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
