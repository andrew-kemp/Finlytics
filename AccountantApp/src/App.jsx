import React, { useState, useEffect, useCallback, useRef } from 'react';
import { ClerkProvider, useAuth, useUser, UserButton, SignInButton } from '@clerk/react';
import AcceptInvite from './components/AcceptInvite';
import Dashboard from './components/Dashboard';

const CLERK_KEY = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY;
const API_BASE = import.meta.env.VITE_API_BASE;

if (!CLERK_KEY) {
  throw new Error('VITE_CLERK_PUBLISHABLE_KEY is not set. Copy .env.example → .env.local and add your Clerk publishable key.');
}

const INVITE_TOKEN = new URLSearchParams(window.location.search).get('token');

export default function App() {
  return (
    <ClerkProvider publishableKey={CLERK_KEY}>
      <Main />
    </ClerkProvider>
  );
}

function Main() {
  const { isSignedIn, isLoaded, getToken } = useAuth();
  const { user } = useUser();
  const [companies, setCompanies] = useState(null);
  const [selected, setSelected] = useState(null);
  const [tab, setTab] = useState('summary');
  const [loadingCompanies, setLoadingCompanies] = useState(true);
  const [error, setError] = useState(null);
  const [showUserMenu, setShowUserMenu] = useState(false);
  const userMenuRef = useRef(null);

  const apiFetch = useCallback(async (path) => {
    const jwt = await getToken();
    const res = await fetch(`${API_BASE}${path}`, {
      headers: { Authorization: `Bearer ${jwt}` },
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || `Request failed (${res.status})`);
    }
    return res.json();
  }, [getToken]);

  // Load companies
  useEffect(() => {
    if (!isSignedIn) return;
    (async () => {
      try {
        const list = await apiFetch('/accountant/companies');
        setCompanies(list);
        if (list.length === 1) setSelected(list[0]);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoadingCompanies(false);
      }
    })();
  }, [isSignedIn, apiFetch]);

  // Close user menu on outside click
  useEffect(() => {
    const handler = (e) => { if (userMenuRef.current && !userMenuRef.current.contains(e.target)) setShowUserMenu(false); };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  if (!isLoaded) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <div className="loading-text">Loading...</div>
      </div>
    );
  }

  if (!isSignedIn) {
    return (
      <div className="auth-wrapper">
        <div className="auth-card">
          <img src="/finlytics-logo.png" alt="Finlytics" className="auth-logo-img" />
          <p className="auth-subtitle">Accountant Portal</p>
          <p style={{ marginBottom: '1rem', color: '#666' }}>
            {INVITE_TOKEN
              ? "You've been invited! Sign in to accept and start viewing company data."
              : 'Sign in to access the accountant portal.'}
          </p>
          <SignInButton mode="modal">
            <button className="btn-primary">
              {INVITE_TOKEN ? 'Accept Invitation' : 'Sign In'}
            </button>
          </SignInButton>
        </div>
      </div>
    );
  }

  // Accept invite flow
  if (INVITE_TOKEN) return <AcceptInvite token={INVITE_TOKEN} />;

  // Main layout
  const userName = user?.fullName || user?.primaryEmailAddress?.emailAddress || 'User';
  const userEmail = user?.primaryEmailAddress?.emailAddress || '';
  const userInitials = userName.split(' ').filter(Boolean).slice(0, 2).map(n => n[0].toUpperCase()).join('');

  const TABS = [
    { key: 'summary', label: '📊 Summary', icon: '📊' },
    { key: 'ledger', label: '📒 Ledger', icon: '📒' },
    { key: 'expenses', label: '💳 Expenses', icon: '💳' },
    { key: 'invoices', label: '📄 Invoices', icon: '📄' },
    { key: 'dividends', label: '💰 Dividends', icon: '💰' },
    { key: 'payroll', label: '👥 Payroll', icon: '👥' },
    { key: 'dla', label: '🏦 DLA', icon: '🏦' },
    { key: 'vat-returns', label: '📋 VAT Returns', icon: '📋' },
  ];

  return (
    <div className="app-container">
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="sidebar-header">
          <img src="/finlytics-logo.png" alt="Finlytics" className="finlytics-logo" />
        </div>

        <ul className="nav-menu">
          <li className="nav-group-label">Company Data</li>
          {TABS.map((t) => (
            <li
              key={t.key}
              className={tab === t.key && selected ? 'active' : ''}
              onClick={() => { if (selected) setTab(t.key); }}
              style={!selected ? { opacity: 0.4, cursor: 'default' } : undefined}
            >
              {t.label}
            </li>
          ))}
          {companies && companies.length > 1 && (
            <>
              <li className="nav-group-label">Switch Company</li>
              {companies.map((c) => (
                <li
                  key={c.companyId}
                  className={selected?.companyId === c.companyId ? 'active' : ''}
                  onClick={() => { setSelected(c); setTab('summary'); }}
                >
                  {c.companyName}
                </li>
              ))}
            </>
          )}
        </ul>

        {/* User card at bottom */}
        <div className="sidebar-user" ref={userMenuRef}>
          <button className="sidebar-user-btn" onClick={() => setShowUserMenu(!showUserMenu)}>
            <div className="user-avatar">{userInitials}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <span className="user-name">{userName}</span>
              <span className="user-email">{userEmail}</span>
            </div>
            <span className="user-chevron">▲</span>
          </button>
          {showUserMenu && (
            <div className="user-menu-dropdown">
              <div style={{ padding: '10px 16px', borderBottom: '1px solid rgba(255,255,255,0.1)' }}>
                <UserButton />
              </div>
            </div>
          )}
        </div>
      </aside>

      {/* Content */}
      <div className="content-wrapper">
        <header className="app-header">
          <div className="company-selector">
            {selected ? (
              <>
                <span className="company-name">{selected.companyName}</span>
                {companies && companies.length > 1 && (
                  <select
                    value={selected.companyId}
                    onChange={(e) => {
                      const c = companies.find(x => String(x.companyId) === e.target.value);
                      if (c) { setSelected(c); setTab('summary'); }
                    }}
                  >
                    {companies.map((c) => (
                      <option key={c.companyId} value={c.companyId}>{c.companyName}</option>
                    ))}
                  </select>
                )}
              </>
            ) : (
              <span className="company-name">Select a company</span>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <span style={{ fontSize: '0.85rem', color: '#6b7280' }}>{userEmail}</span>
          </div>
        </header>

        <main className="content">
          {loadingCompanies ? (
            <div className="loading-container">
              <div className="spinner"></div>
              <div className="loading-text">Loading companies...</div>
            </div>
          ) : error ? (
            <div className="status-error">{error}</div>
          ) : !selected ? (
            <>
              <div className="page-header">
                <h2>Your Companies</h2>
              </div>
              {companies && companies.length === 0 ? (
                <div className="status-empty">No companies linked to your account yet.</div>
              ) : (
                <div className="company-list">
                  {companies?.map((c) => (
                    <div key={c.companyId} className="company-card" onClick={() => { setSelected(c); setTab('summary'); }}>
                      <h2>{c.companyName}</h2>
                      <p>Access: <span className="status-badge status-active">{c.accessLevel}</span></p>
                    </div>
                  ))}
                </div>
              )}
            </>
          ) : (
            <Dashboard
              company={selected}
              tab={tab}
              setTab={setTab}
              apiFetch={apiFetch}
            />
          )}
        </main>
      </div>
    </div>
  );
}
