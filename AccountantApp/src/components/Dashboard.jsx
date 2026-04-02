import { useState, useEffect } from 'react';

const TAB_LABELS = {
  summary: 'Summary',
  ledger: 'Ledger',
  expenses: 'Expenses',
  invoices: 'Invoices',
  dividends: 'Dividends',
  payroll: 'Payroll',
  dla: 'DLA',
  'vat-returns': 'VAT Returns',
};

function fmt(val) {
  if (val == null) return '—';
  if (typeof val === 'number') return val.toLocaleString('en-GB', { style: 'currency', currency: 'GBP' });
  return String(val);
}

function fmtDate(val) {
  if (!val) return '—';
  return new Date(val).toLocaleDateString('en-GB');
}

export default function Dashboard({ company, tab, setTab, apiFetch }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Load tab data when company/tab changes
  useEffect(() => {
    if (!company) return;
    setData(null);
    setError(null);
    setLoading(true);
    (async () => {
      try {
        const result = await apiFetch(`/accountant/company/${company.companyId}/${tab}`);
        setData(result);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [company, tab, apiFetch]);

  const label = TAB_LABELS[tab] || tab;

  return (
    <div className="dashboard-content">
      <div className="page-header">
        <h2>{label}</h2>
      </div>

      {loading ? (
        <div className="loading-container">
          <div className="spinner"></div>
          <div className="loading-text">Loading {label.toLowerCase()}...</div>
        </div>
      ) : error ? (
        <div className="status-error">{error}</div>
      ) : tab === 'summary' ? (
        <SummaryView data={data} />
      ) : (
        <TableView data={data} tab={tab} />
      )}
    </div>
  );
}

function SummaryView({ data }) {
  if (!data) return null;
  const cards = Object.entries(data).filter(([, v]) => typeof v !== 'object');
  return (
    <div className="metrics-grid">
      {cards.map(([key, value]) => (
        <div key={key} className="metric-card">
          <div className="label">{key.replace(/([A-Z])/g, ' $1').trim()}</div>
          <div className="value">{fmt(value)}</div>
        </div>
      ))}
    </div>
  );
}

function TableView({ data, tab }) {
  const rows = Array.isArray(data) ? data : data?.items ?? data?.entries ?? data?.runs ?? [];
  if (rows.length === 0) return <div className="status-empty">No {TAB_LABELS[tab] || tab} records found.</div>;

  const cols = Object.keys(rows[0]).filter((k) => !k.startsWith('_'));

  return (
    <div style={{ overflowX: 'auto' }}>
      <p className="table-count">{rows.length} record{rows.length !== 1 ? 's' : ''}</p>
      <table className="data-table">
        <thead>
          <tr>
            {cols.map((c) => (
              <th key={c}>{c.replace(/([A-Z])/g, ' $1').trim()}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i}>
              {cols.map((c) => (
                <td key={c}>
                  {typeof row[c] === 'number' ? fmt(row[c])
                    : String(row[c] ?? '').match(/^\d{4}-\d{2}-\d{2}/) ? fmtDate(row[c])
                    : String(row[c] ?? '—')}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
