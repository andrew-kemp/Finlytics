import React, { useEffect, useState } from 'react';
import { getBankAccounts, createBankAccount, updateBankAccount, deleteBankAccount, getBankTransactionsByAccount, createBankTransaction, getMonzoStatus, getMonzoAuthUrl, syncMonzoTransactions } from '../services/apiService';

const defaultAccount = {
    accountName: '',
    bankName: 'Monzo',
    sortCode: '',
    accountNumber: '',
    currency: 'GBP',
    openingBalance: '',
    isActive: true,
    notes: ''
};

const defaultTransaction = {
    transactionDate: '',
    amount: '',
    description: '',
    reference: '',
    category: '',
    direction: 'Out'
};

export default function Banking() {
    const [accounts, setAccounts] = useState([]);
    const [selectedAccount, setSelectedAccount] = useState(null);
    const [transactions, setTransactions] = useState([]);
    const [loading, setLoading] = useState(true);
    const [processing, setProcessing] = useState(false);
    const [showAccountForm, setShowAccountForm] = useState(false);
    const [showTransactionForm, setShowTransactionForm] = useState(false);
    const [editingAccount, setEditingAccount] = useState(null);
    const [accountForm, setAccountForm] = useState(defaultAccount);
    const [transactionForm, setTransactionForm] = useState(defaultTransaction);
    const [monzoStatus, setMonzoStatus] = useState(null);
    const [syncing, setSyncing] = useState(false);
    const [syncResult, setSyncResult] = useState(null);

    useEffect(() => {
        loadAccounts();
        getMonzoStatus().then(setMonzoStatus).catch(() => setMonzoStatus({ connected: false }));

        // Handle redirect back from Monzo OAuth
        const params = new URLSearchParams(window.location.search);
        console.log('Banking mount — URL params:', window.location.search);
        if (params.get('monzo_connected') === 'true') {
            console.log('Monzo OAuth success!');
            setSyncResult({ success: true, message: 'Monzo connected successfully!' });
            window.history.replaceState({}, '', window.location.pathname);
            setTimeout(() => getMonzoStatus().then(setMonzoStatus).catch(() => {}), 1500);
        } else if (params.get('monzo_error')) {
            const errMsg = decodeURIComponent(params.get('monzo_error'));
            console.error('Monzo OAuth error:', errMsg);
            setSyncResult({ success: false, message: `Monzo connection failed: ${errMsg}` });
            window.history.replaceState({}, '', window.location.pathname);
        }
    }, []);

    async function loadAccounts() {
        try {
            const data = await getBankAccounts();
            setAccounts(data);
            if (data.length > 0) {
                setSelectedAccount(data[0]);
                await loadTransactions(data[0].id);
            }
        } catch (error) {
            console.error('Error loading accounts:', error);
        } finally {
            setLoading(false);
        }
    }

    async function loadTransactions(accountId) {
        try {
            const data = await getBankTransactionsByAccount(accountId);
            setTransactions(data);
        } catch (error) {
            console.error('Error loading transactions:', error);
        }
    }

    const handleSelectAccount = async (account) => {
        setSelectedAccount(account);
        await loadTransactions(account.id);
    };

    const handleNewAccount = () => {
        setAccountForm(defaultAccount);
        setEditingAccount(null);
        setShowAccountForm(true);
    };

    const handleEditAccount = (account) => {
        setEditingAccount(account);
        setAccountForm({
            accountName: account.accountName || '',
            bankName: account.bankName || 'Monzo',
            sortCode: account.sortCode || '',
            accountNumber: account.accountNumber || '',
            currency: account.currency || 'GBP',
            openingBalance: account.openingBalance ?? '',
            isActive: account.isActive !== false,
            notes: account.notes || ''
        });
        setShowAccountForm(true);
    };

    const handleSaveAccount = async (e) => {
        e.preventDefault();
        setProcessing(true);
        try {
            const payload = {
                ...accountForm,
                openingBalance: accountForm.openingBalance === '' ? null : Number(accountForm.openingBalance)
            };

            if (editingAccount) {
                await updateBankAccount(editingAccount.id, payload);
            } else {
                await createBankAccount(payload);
            }

            await loadAccounts();
            setShowAccountForm(false);
        } catch (error) {
            console.error('Error saving account:', error);
            alert('Failed to save account: ' + error.message);
        } finally {
            setProcessing(false);
        }
    };

    const handleDeleteAccount = async (account) => {
        if (!confirm(`Delete bank account "${account.accountName}"?`)) return;
        setProcessing(true);
        try {
            await deleteBankAccount(account.id);
            await loadAccounts();
        } catch (error) {
            console.error('Error deleting account:', error);
            alert('Failed to delete account: ' + error.message);
        } finally {
            setProcessing(false);
        }
    };

    const handleNewTransaction = () => {
        setTransactionForm(defaultTransaction);
        setShowTransactionForm(true);
    };

    const handleSaveTransaction = async (e) => {
        e.preventDefault();
        if (!selectedAccount) return;
        setProcessing(true);
        try {
            const payload = {
                ...transactionForm,
                bankAccountId: selectedAccount.id,
                amount: transactionForm.amount === '' ? null : Number(transactionForm.amount),
                transactionDate: transactionForm.transactionDate || null,
                source: 'Manual'
            };

            await createBankTransaction(payload);
            await loadTransactions(selectedAccount.id);
            setShowTransactionForm(false);
        } catch (error) {
            console.error('Error saving transaction:', error);
            alert('Failed to save transaction: ' + error.message);
        } finally {
            setProcessing(false);
        }
    };

    const handleMonzoConnect = async () => {
        try {
            console.log('Monzo connect: fetching auth URL...');
            const data = await getMonzoAuthUrl();
            console.log('Monzo connect: got auth URL', data.authUrl);
            window.location.href = data.authUrl;
        } catch (err) {
            console.error('Monzo connect error:', err);
            setSyncResult({ success: false, message: 'Could not start Monzo connection: ' + err.message });
        }
    };

    const handleMonzoSync = async () => {
        setSyncing(true);
        setSyncResult(null);
        try {
            const result = await syncMonzoTransactions();
            setSyncResult({ success: true, message: result.message, imported: result.imported });
            // Refresh status and transactions
            getMonzoStatus().then(setMonzoStatus).catch(() => {});
            await loadAccounts();
            if (selectedAccount) await loadTransactions(selectedAccount.id);
        } catch (err) {
            setSyncResult({ success: false, message: err.message });
        } finally {
            setSyncing(false);
        }
    };

    return (
        <div className="content-container">
            <div className="section-header">
                <h2>Banking</h2>
                <button className="btn-primary" onClick={handleNewAccount} disabled={processing}>
                    + Add Bank Account
                </button>
            </div>

            {/* ── Monzo Integration Panel ── */}
            {monzoStatus && (
                <div style={{
                    background: monzoStatus.connected ? '#f0fdf4' : '#fafafa',
                    border: `1px solid ${monzoStatus.connected ? '#bbf7d0' : '#e5e7eb'}`,
                    borderRadius: 10, padding: '1rem 1.25rem', marginBottom: '1.25rem',
                    display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem'
                }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                        <span style={{ fontSize: '1.5rem' }}>🏦</span>
                        <div>
                            <div style={{ fontWeight: 700, fontSize: '0.95rem', color: monzoStatus.connected ? '#15803d' : '#374151' }}>
                                {monzoStatus.connected ? '✓ Monzo Connected — ANDY KEMP CONSULTING LTD' : 'Monzo Not Connected'}
                            </div>
                            {monzoStatus.connected && (
                                <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: 2 }}>
                                    Balance: <strong>£{parseFloat(monzoStatus.balance || 0).toFixed(2)}</strong>
                                    {monzoStatus.totalBalance !== monzoStatus.balance && (
                                        <span style={{ marginLeft: 8 }}>· Total (inc pots): <strong>£{parseFloat(monzoStatus.totalBalance || 0).toFixed(2)}</strong></span>
                                    )}
                                    {monzoStatus.lastSyncedAt && (
                                        <span style={{ marginLeft: 8 }}>· Last sync: {new Date(monzoStatus.lastSyncedAt).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}</span>
                                    )}
                                    {monzoStatus.tokenExpired && (
                                        <span style={{ marginLeft: 8, color: '#dc2626', fontWeight: 600 }}>⚠ Token expired — re-authenticate at developers.monzo.com</span>
                                    )}
                                </div>
                            )}
                        </div>
                    </div>
                    {!monzoStatus.connected && (
                        <button
                            onClick={handleMonzoConnect}
                            style={{
                                background: '#ef4444', color: '#fff', border: 'none',
                                borderRadius: 6, padding: '0.5rem 1.1rem',
                                fontWeight: 600, fontSize: '0.875rem', cursor: 'pointer'
                            }}
                        >
                            🔗 Connect Monzo
                        </button>
                    )}
                    {monzoStatus.connected && !monzoStatus.tokenExpired && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                            {syncResult && (
                                <span style={{ fontSize: '0.82rem', color: syncResult.success ? '#15803d' : '#dc2626', fontWeight: 500 }}>
                                    {syncResult.success ? `✓ ${syncResult.message}` : `✗ ${syncResult.message}`}
                                </span>
                            )}
                            <button
                                onClick={handleMonzoSync}
                                disabled={syncing}
                                style={{
                                    background: syncing ? '#d1fae5' : '#16a34a', color: '#fff',
                                    border: 'none', borderRadius: 6, padding: '0.45rem 1rem',
                                    fontWeight: 600, fontSize: '0.875rem', cursor: syncing ? 'not-allowed' : 'pointer',
                                    display: 'flex', alignItems: 'center', gap: '0.4rem'
                                }}
                            >
                                {syncing ? '⏳ Syncing…' : '🔄 Sync Monzo'}
                            </button>
                            <button
                                onClick={handleMonzoConnect}
                                style={{
                                    background: 'transparent', color: '#6b7280', border: '1px solid #d1d5db',
                                    borderRadius: 6, padding: '0.45rem 0.85rem',
                                    fontWeight: 500, fontSize: '0.8rem', cursor: 'pointer'
                                }}
                            >
                                🔗 Reconnect
                            </button>
                        </div>
                    )}
                    {monzoStatus.connected && monzoStatus.tokenExpired && (
                        <button
                            onClick={handleMonzoConnect}
                            style={{
                                background: '#ef4444', color: '#fff', border: 'none',
                                borderRadius: 6, padding: '0.5rem 1.1rem',
                                fontWeight: 600, fontSize: '0.875rem', cursor: 'pointer'
                            }}
                        >
                            🔗 Re-connect Monzo
                        </button>
                    )}
                </div>
            )}

            {/* ── Sync / Connection Result Banner ── */}
            {syncResult && (
                <div style={{
                    background: syncResult.success ? '#f0fdf4' : '#fef2f2',
                    border: `1px solid ${syncResult.success ? '#bbf7d0' : '#fca5a5'}`,
                    borderRadius: 8, padding: '0.6rem 1rem', marginBottom: '1rem',
                    color: syncResult.success ? '#15803d' : '#dc2626',
                    fontWeight: 500, fontSize: '0.875rem',
                    display: 'flex', justifyContent: 'space-between', alignItems: 'center'
                }}>
                    <span>{syncResult.success ? '✓' : '✗'} {syncResult.message}</span>
                    <button onClick={() => setSyncResult(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '1rem', color: '#6b7280' }}>✕</button>
                </div>
            )}

            {showAccountForm && (
                <div className="form-card">
                    <h3>{editingAccount ? 'Edit Bank Account' : 'New Bank Account'}</h3>
                    <form onSubmit={handleSaveAccount}>
                        <div className="form-grid">
                            <div className="form-group">
                                <label>Account Name</label>
                                <input value={accountForm.accountName} onChange={e => setAccountForm({ ...accountForm, accountName: e.target.value })} required />
                            </div>
                            <div className="form-group">
                                <label>Bank Name</label>
                                <input value={accountForm.bankName} onChange={e => setAccountForm({ ...accountForm, bankName: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label>Sort Code</label>
                                <input value={accountForm.sortCode} onChange={e => setAccountForm({ ...accountForm, sortCode: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label>Account Number</label>
                                <input value={accountForm.accountNumber} onChange={e => setAccountForm({ ...accountForm, accountNumber: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label>Currency</label>
                                <input value={accountForm.currency} onChange={e => setAccountForm({ ...accountForm, currency: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label>Opening Balance</label>
                                <input type="number" step="0.01" value={accountForm.openingBalance} onChange={e => setAccountForm({ ...accountForm, openingBalance: e.target.value })} />
                            </div>
                            <div className="form-group full-width">
                                <label>Notes</label>
                                <textarea value={accountForm.notes} onChange={e => setAccountForm({ ...accountForm, notes: e.target.value })} />
                            </div>
                        </div>
                        <div className="form-actions">
                            <button type="submit" className="btn-primary" disabled={processing}>Save</button>
                            <button type="button" className="btn-secondary" onClick={() => setShowAccountForm(false)}>Cancel</button>
                        </div>
                    </form>
                </div>
            )}

            {loading ? (
                <div className="loading">Loading...</div>
            ) : (
                <div className="table-container">
                    <table className="data-table">
                        <thead>
                            <tr>
                                <th>Account</th>
                                <th>Bank</th>
                                <th>Currency</th>
                                <th>Active</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {accounts.map(account => (
                                <tr key={account.id}>
                                    <td>
                                        <button className="btn-link" onClick={() => handleSelectAccount(account)}>
                                            {account.accountName}
                                        </button>
                                    </td>
                                    <td>{account.bankName}</td>
                                    <td>{account.currency}</td>
                                    <td>{account.isActive ? 'Yes' : 'No'}</td>
                                    <td>
                                        <button className="btn-secondary" onClick={() => handleEditAccount(account)}>Edit</button>
                                        <button className="btn-danger" onClick={() => handleDeleteAccount(account)}>Delete</button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {selectedAccount && (
                <div style={{ marginTop: '2rem' }}>
                    <div className="section-header">
                        <h3>Transactions - {selectedAccount.accountName}</h3>
                        <button className="btn-primary" onClick={handleNewTransaction}>+ Add Transaction</button>
                    </div>

                    {showTransactionForm && (
                        <div className="form-card">
                            <h4>New Transaction</h4>
                            <form onSubmit={handleSaveTransaction}>
                                <div className="form-grid">
                                    <div className="form-group">
                                        <label>Date</label>
                                        <input type="date" value={transactionForm.transactionDate} onChange={e => setTransactionForm({ ...transactionForm, transactionDate: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label>Amount</label>
                                        <input type="number" step="0.01" value={transactionForm.amount} onChange={e => setTransactionForm({ ...transactionForm, amount: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label>Direction</label>
                                        <select value={transactionForm.direction} onChange={e => setTransactionForm({ ...transactionForm, direction: e.target.value })}>
                                            <option>In</option>
                                            <option>Out</option>
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label>Description</label>
                                        <input value={transactionForm.description} onChange={e => setTransactionForm({ ...transactionForm, description: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label>Reference</label>
                                        <input value={transactionForm.reference} onChange={e => setTransactionForm({ ...transactionForm, reference: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label>Category</label>
                                        <input value={transactionForm.category} onChange={e => setTransactionForm({ ...transactionForm, category: e.target.value })} />
                                    </div>
                                </div>
                                <div className="form-actions">
                                    <button type="submit" className="btn-primary">Save</button>
                                    <button type="button" className="btn-secondary" onClick={() => setShowTransactionForm(false)}>Cancel</button>
                                </div>
                            </form>
                        </div>
                    )}

                    <div className="table-container">
                        <table className="data-table">
                            <thead>
                                <tr>
                                    <th>Date</th>
                                    <th>Description</th>
                                    <th>Amount</th>
                                    <th>Direction</th>
                                    <th>Reconciled</th>
                                </tr>
                            </thead>
                            <tbody>
                                {transactions.map(tx => (
                                    <tr key={tx.id}>
                                        <td>{tx.transactionDate ? tx.transactionDate.substring(0, 10) : ''}</td>
                                        <td>{tx.description}</td>
                                        <td>{tx.amount ? `£${tx.amount}` : ''}</td>
                                        <td>{tx.direction}</td>
                                        <td>{tx.isReconciled ? 'Yes' : 'No'}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}
        </div>
    );
}
