import { useAuth, useUser } from '@clerk/react'
import { useState, useEffect } from 'react'
import { useSearchParams, useNavigate } from 'react-router-dom'
import { acceptInvite } from '../services/api'

export default function AcceptInvite() {
  const { getToken, isSignedIn } = useAuth()
  const { user } = useUser()
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const token = searchParams.get('token')

  const [status, setStatus] = useState('idle') // idle | accepting | success | error | no-token
  const [message, setMessage] = useState('')
  const [company, setCompany] = useState(null)

  useEffect(() => {
    if (!token) {
      setStatus('no-token')
      return
    }
    if (!isSignedIn) return // wait for sign-in

    async function accept() {
      setStatus('accepting')
      try {
        const result = await acceptInvite(getToken, token)
        setStatus('success')
        setCompany(result.companyName || 'your company')
      } catch (err) {
        setStatus('error')
        setMessage(err.message || 'Failed to accept invite. The link may have expired or already been used.')
      }
    }
    accept()
  }, [token, isSignedIn, getToken])

  if (status === 'no-token') {
    return (
      <div className="invite-page">
        <div className="invite-card">
          <div className="invite-icon">❌</div>
          <h1>Invalid Invite Link</h1>
          <p>No invitation token was found in this link. Please check the link you received in your email.</p>
          <button className="btn-primary" onClick={() => navigate('/')}>Go to Dashboard</button>
        </div>
      </div>
    )
  }

  if (!isSignedIn) {
    return (
      <div className="invite-page">
        <div className="invite-card">
          <div className="invite-icon">🔑</div>
          <h1>Sign In Required</h1>
          <p>You need to sign in to accept this invitation. Once signed in, the invitation will be accepted automatically.</p>
        </div>
      </div>
    )
  }

  if (status === 'accepting') {
    return (
      <div className="invite-page">
        <div className="invite-card">
          <div className="invite-icon spin">⏳</div>
          <h1>Accepting Invitation...</h1>
          <p>Setting up your account, {user?.firstName || 'there'}...</p>
        </div>
      </div>
    )
  }

  if (status === 'success') {
    return (
      <div className="invite-page">
        <div className="invite-card success">
          <div className="invite-icon">🎉</div>
          <h1>Welcome aboard!</h1>
          <p>You've successfully joined <strong>{company}</strong>.</p>
          <p>You can now submit expenses and log mileage trips.</p>
          <button className="btn-primary" onClick={() => navigate('/')}>Go to Dashboard</button>
        </div>
      </div>
    )
  }

  if (status === 'error') {
    return (
      <div className="invite-page">
        <div className="invite-card error">
          <div className="invite-icon">⚠️</div>
          <h1>Invitation Failed</h1>
          <p>{message}</p>
          <button className="btn-primary" onClick={() => navigate('/')}>Go to Dashboard</button>
        </div>
      </div>
    )
  }

  return null
}
