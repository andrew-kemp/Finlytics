const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:7071/api';

async function authFetch(url, options = {}, getToken) {
  const token = await getToken();
  const headers = {
    ...options.headers,
    'Authorization': `Bearer ${token}`,
  };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }
  const res = await fetch(url, { ...options, headers });
  if (!res.ok) {
    const text = await res.text();
    let msg = `Request failed (${res.status})`;
    try { msg = JSON.parse(text).error || msg; } catch { /* */ }
    throw new Error(msg);
  }
  if (res.status === 204) return null;
  return res.json();
}

// ── Profile ──
export const getProfile = (getToken) =>
  authFetch(`${API_BASE}/employee/me`, {}, getToken);

// ── Accept Invite ──
export const acceptInvite = (inviteToken, getToken) =>
  authFetch(`${API_BASE}/employee/accept-invite`, {
    method: 'POST',
    body: JSON.stringify({ inviteToken }),
  }, getToken);

// ── Expenses ──
export const getExpenses = (getToken) =>
  authFetch(`${API_BASE}/employee/expenses`, {}, getToken);

export const createExpense = (expense, getToken) =>
  authFetch(`${API_BASE}/employee/expenses`, {
    method: 'POST',
    body: JSON.stringify(expense),
  }, getToken);

export const updateExpense = (id, expense, getToken) =>
  authFetch(`${API_BASE}/employee/expenses/${id}`, {
    method: 'PUT',
    body: JSON.stringify(expense),
  }, getToken);

export const deleteExpense = (id, getToken) =>
  authFetch(`${API_BASE}/employee/expenses/${id}`, {
    method: 'DELETE',
  }, getToken);

export const uploadReceipt = async (expenseId, file, getToken) => {
  const formData = new FormData();
  formData.append('file', file, file.name);
  return authFetch(`${API_BASE}/employee/expenses/${expenseId}/upload`, {
    method: 'POST',
    body: formData,
  }, getToken);
};

// ── OCR Scan (shared endpoint with admin app) ──
export const analyzeReceipt = async (file, getToken) => {
  const formData = new FormData();
  formData.append('file', file, file.name);
  const token = await getToken();
  const res = await fetch(`${API_BASE}/analyze-invoice`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: formData,
  });
  if (!res.ok) return null;
  return res.json();
};

// ── Mileage ──
export const getMileage = (getToken) =>
  authFetch(`${API_BASE}/employee/mileage`, {}, getToken);

export const createMileage = (trip, getToken) =>
  authFetch(`${API_BASE}/employee/mileage`, {
    method: 'POST',
    body: JSON.stringify(trip),
  }, getToken);

// ── Mileage Tracker ──
export const getMileageTracker = (getToken) =>
  authFetch(`${API_BASE}/employee/mileage-tracker`, {}, getToken);
