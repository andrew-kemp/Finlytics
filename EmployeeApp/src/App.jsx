import { Routes, Route, NavLink, Navigate, useSearchParams } from 'react-router-dom'
import { ClerkLoaded, ClerkLoading, SignInButton, UserButton, useAuth, useUser } from '@clerk/react'
import { useState } from 'react'
import Dashboard from './pages/Dashboard'
import Expenses from './pages/Expenses'
import Mileage from './pages/Mileage'
import MileageTracker from './pages/MileageTracker'
import AcceptInvite from './pages/AcceptInvite'
import './App.css'

function App() {
  const { isSignedIn, isLoaded } = useAuth()

  if (!isLoaded) {
    return (
      <div className="loading">
        <span>Loading...</span>
      </div>
    )
  }

  return isSignedIn ? <AppShell /> : <LandingPage />
}

function LandingPage() {
  const [searchParams] = useSearchParams()
  const hasInvite = searchParams.get('token')

  return (
    <div className="landing">
      <div className="landing-card">
        <div className="landing-logo">
          <span className="logo-icon">💰</span>
          <h1>Finlytics <span className="subtitle">Expenses</span></h1>
        </div>
        <p className="landing-desc">
          {hasInvite
            ? "You've been invited! Sign in to accept and start submitting expenses."
            : 'Submit expenses, log mileage, and track your HMRC allowance.'}
        </p>
        <SignInButton mode="modal">
          <button className="btn btn-primary btn-lg">
            {hasInvite ? 'Accept Invitation' : 'Sign In'}
          </button>
        </SignInButton>
      </div>
    </div>
  )
}

function AppShell() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="header-left">
          <button className="menu-toggle" onClick={() => setMobileMenuOpen(!mobileMenuOpen)}>
            ☰
          </button>
          <span className="header-logo">💰 Finlytics Expenses</span>
        </div>
        <nav className={`header-nav ${mobileMenuOpen ? 'open' : ''}`}>
          <NavLink to="/" end onClick={() => setMobileMenuOpen(false)}>Dashboard</NavLink>
          <NavLink to="/expenses" onClick={() => setMobileMenuOpen(false)}>Expenses</NavLink>
          <NavLink to="/mileage" onClick={() => setMobileMenuOpen(false)}>Mileage</NavLink>
          <NavLink to="/mileage-tracker" onClick={() => setMobileMenuOpen(false)}>Tracker</NavLink>
        </nav>
        <div className="header-right">
          <UserButton afterSignOutUrl="/" />
        </div>
      </header>

      <main className="app-main">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/expenses" element={<Expenses />} />
          <Route path="/mileage" element={<Mileage />} />
          <Route path="/mileage-tracker" element={<MileageTracker />} />
          <Route path="/accept-invite" element={<AcceptInvite />} />
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </main>
    </div>
  )
}

export default App