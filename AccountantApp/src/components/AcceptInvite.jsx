import { useAuth } from '@clerk/react';
import { useState, useEffect } from 'react';

const API_BASE = import.meta.env.VITE_API_BASE;

export default function AcceptInvite({ token }) {
  const { getToken } = useAuth();
  const [status, setStatus] = useState('accepting');
  const [message, setMessage] = useState('');

  useEffect(() => {
    async function accept() {
      try {
        const jwt = await getToken();
        const res = await fetch(`${API_BASE}/accountant/accept-invite`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${jwt}`,
          },
          body: JSON.stringify({ inviteToken: token }),
        });

        if (!res.ok) {
          const err = await res.json().catch(() => ({}));
          throw new Error(err.error || `Request failed (${res.status})`);
        }

        setStatus('success');
      } catch (err) {
        setStatus('error');
        setMessage(err.message || 'Failed to accept invite. The link may have expired or already been used.');
      }
    }
    accept();
  }, [token, getToken]);

  if (status === 'accepting') {
    return (
      <div className="invite-page">
        <div className="invite-card">
          <div className="invite-icon spin">&#8987;</div>
          <h1>Accepting Invitation...</h1>
          <p>Setting up your account...</p>
        </div>
      </div>
    );
  }

  if (status === 'error') {
    return (
      <div className="invite-page">
        <div className="invite-card">
          <div className="invite-icon">&#10060;</div>
          <h1>Something went wrong</h1>
          <p>{message}</p>
          <button className="btn-primary" onClick={() => { window.location.href = window.location.pathname; }}>
            Go to Dashboard
          </button>
        </div>
      </div>
    );
  }

  // success – redirect to dashboard (strip token from URL)
  return (
    <div className="invite-page">
      <div className="invite-card success">
        <div className="invite-icon">&#127881;</div>
        <h1>Welcome!</h1>
        <p>Your invitation has been accepted. You now have read-only access.</p>
        <button className="btn-primary" onClick={() => { window.location.href = window.location.pathname; }}>
          Go to Dashboard
        </button>
      </div>
    </div>
  );
}
