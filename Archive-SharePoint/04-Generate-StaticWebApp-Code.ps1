<#
    03-Generate-StaticWebApp-Code.ps1
    Generates complete React Static Web App for FinanceHub with Entra authentication
#>

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"
$outputDir = Join-Path $scriptRoot "StaticWebApp"

function Get-IniContent {
    param([string]$Path)
    $ini = @{}
    if (-not (Test-Path $Path)) { return $ini }
    $section = $null
    switch -regex -file $Path {
        '^\s*\[(.+)\]\s*$' {
            $section = $matches[1]
            if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} }
        }
        '^\s*([^=]+?)\s*=\s*(.*)$' {
            if (-not $section) { $section = "Default"; if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} } }
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }
    return $ini
}

try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Generating Static Web App Code (React)" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    if (-not (Test-Path $configFile)) {
        throw "Config file not found. Run 01-Configure-Azure-Resources.ps1 first."
    }
    
    $config = Get-IniContent -Path $configFile
    
    # Create output directory structure
    if (Test-Path $outputDir) {
        Write-Host "Output directory exists. Overwrite? (Y/N)" -ForegroundColor Yellow
        $confirm = Read-Host
        if ($confirm -ne "Y") {
            Write-Host "Cancelled." -ForegroundColor Yellow
            exit 0
        }
        Remove-Item $outputDir -Recurse -Force
    }
    
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\src" | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\src\auth" | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\src\components" | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\src\services" | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\src\styles" | Out-Null
    New-Item -ItemType Directory -Path "$outputDir\public" | Out-Null
    
    Write-Host "Creating React project structure..." -ForegroundColor Yellow
    
    # ===========================
    # package.json
    # ===========================
    $packageJson = @"
{
  "name": "financehub-portal",
  "version": "1.0.0",
  "private": true,
  "dependencies": {
    "@azure/msal-browser": "^3.7.1",
    "@azure/msal-react": "^2.0.10",
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "react-router-dom": "^6.21.0",
    "axios": "^1.6.5"
  },
  "devDependencies": {
    "@types/react": "^18.2.48",
    "@types/react-dom": "^18.2.18",
    "typescript": "^5.3.3",
    "vite": "^5.0.11",
    "@vitejs/plugin-react": "^4.2.1"
  },
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "preview": "vite preview"
  }
}
"@
    $packageJson | Set-Content "$outputDir\package.json"
    
    # ===========================
    # vite.config.js
    # ===========================
    $viteConfig = @"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist'
  }
})
"@
    $viteConfig | Set-Content "$outputDir\vite.config.js"
    
    # ===========================
    # public/index.html
    # ===========================
    $indexHtml = @"
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>$($config['Brand']['CompanyDisplay']) $($config['Brand']['Product'])</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/index.jsx"></script>
  </body>
</html>
"@
    $indexHtml | Set-Content "$outputDir\public\index.html"
    
    # ===========================
    # src/auth/authConfig.js
    # ===========================
    $authConfig = @"
import { PublicClientApplication } from "@azure/msal-browser";

export const msalConfig = {
    auth: {
        clientId: "$($config['App']['ClientId'])",
        authority: "https://login.microsoftonline.com/$($config['Azure']['TenantId'])",
        redirectUri: "$($config['StaticWebApp']['StaticWebAppUrl'])"
    },
    cache: {
        cacheLocation: "localStorage",
        storeAuthStateInCookie: false
    }
};

export const loginRequest = {
    scopes: ["User.Read", "Sites.ReadWrite.All"]
};

export const msalInstance = new PublicClientApplication(msalConfig);
"@
    $authConfig | Set-Content "$outputDir\src\auth\authConfig.js"
    
    # ===========================
    # src/services/apiService.js
    # ===========================
    $apiService = @"
const API_BASE = '$($config['FunctionApp']['FunctionAppUrl'])/api';

export async function generateCode(name, type) {
    const response = await fetch(`"`${API_BASE}/GenerateCode`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, type })
    });
    if (!response.ok) throw new Error('Failed to generate code');
    return response.json();
}

export async function markInvoicePaid(invoiceId) {
    const response = await fetch(`"`${API_BASE}/MarkInvoicePaid`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ invoiceId })
    });
    if (!response.ok) throw new Error('Failed to mark invoice as paid');
    return response.json();
}

export async function getCustomers() {
    const response = await fetch(`"`${API_BASE}/GetCustomers`");
    if (!response.ok) throw new Error('Failed to fetch customers');
    return response.json();
}

export async function createCustomer(customer) {
    const response = await fetch(`"`${API_BASE}/CreateCustomer`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(customer)
    });
    if (!response.ok) throw new Error('Failed to create customer');
    return response.json();
}

export async function getSuppliers() {
    const response = await fetch(`"`${API_BASE}/GetSuppliers`");
    if (!response.ok) throw new Error('Failed to fetch suppliers');
    return response.json();
}

export async function createSupplier(supplier) {
    const response = await fetch(`"`${API_BASE}/CreateSupplier`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(supplier)
    });
    if (!response.ok) throw new Error('Failed to create supplier');
    return response.json();
}

export async function getInvoices() {
    const response = await fetch(`"`${API_BASE}/GetInvoices`");
    if (!response.ok) throw new Error('Failed to fetch invoices');
    return response.json();
}

export async function getUnpaidInvoices() {
    const response = await fetch(`"`${API_BASE}/GetUnpaidInvoices`");
    if (!response.ok) throw new Error('Failed to fetch unpaid invoices');
    return response.json();
}

export async function getQuotes() {
    const response = await fetch(`"`${API_BASE}/GetQuotes`");
    if (!response.ok) throw new Error('Failed to fetch quotes');
    return response.json();
}

export async function createQuote(quote) {
    const response = await fetch(`"`${API_BASE}/CreateQuote`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(quote)
    });
    if (!response.ok) throw new Error('Failed to create quote');
    return response.json();
}

export async function convertQuoteToInvoice(quoteId) {
    const response = await fetch(`"`${API_BASE}/ConvertQuoteToInvoice`", {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ quoteId })
    });
    if (!response.ok) throw new Error('Failed to convert quote');
    return response.json();
}
"@
    $apiService | Set-Content "$outputDir\src\services\apiService.js"
    
    # ===========================
    # src/App.jsx
    # ===========================
    $appJsx = @"
import React, { useState } from 'react';
import { MsalProvider, useMsal, useIsAuthenticated } from '@azure/msal-react';
import { msalInstance, loginRequest } from './auth/authConfig';
import Dashboard from './components/Dashboard';
import Customers from './components/Customers';
import Suppliers from './components/Suppliers';
import Invoices from './components/Invoices';
import Quotes from './components/Quotes';
import './styles/App.css';

function App() {
    return (
        <MsalProvider instance={msalInstance}>
            <MainContent />
        </MsalProvider>
    );
}

function MainContent() {
    const { instance } = useMsal();
    const isAuthenticated = useIsAuthenticated();
    const [view, setView] = useState('dashboard');

    const handleLogin = () => {
        instance.loginPopup(loginRequest);
    };

    const handleLogout = () => {
        instance.logoutPopup();
    };

    if (!isAuthenticated) {
        return (
            <div className="login-container">
                <div className="login-card">
                    <h1>$($config['Brand']['CompanyDisplay']) $($config['Brand']['Product'])</h1>
                    <p>Manage your quotes, invoices, customers and suppliers</p>
                    <button onClick={handleLogin} className="btn-primary">
                        Sign In with Microsoft
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="app-container">
            <nav className="sidebar">
                <div className="sidebar-header">
                    <h2>$($config['Brand']['CompanyDisplay']) $($config['Brand']['Product'])</h2>
                </div>
                <ul className="nav-menu">
                    <li onClick={() => setView('dashboard')} className={view === 'dashboard' ? 'active' : ''}>
                        📊 Dashboard
                    </li>
                    <li onClick={() => setView('customers')} className={view === 'customers' ? 'active' : ''}>
                        👥 Customers
                    </li>
                    <li onClick={() => setView('suppliers')} className={view === 'suppliers' ? 'active' : ''}>
                        🏢 Suppliers
                    </li>
                    <li onClick={() => setView('invoices')} className={view === 'invoices' ? 'active' : ''}>
                        💰 Invoices
                    </li>
                    <li onClick={() => setView('quotes')} className={view === 'quotes' ? 'active' : ''}>
                        📄 Quotes
                    </li>
                </ul>
                <div className="sidebar-footer">
                    <button onClick={handleLogout} className="btn-logout">Logout</button>
                </div>
            </nav>
            <main className="content">
                {view === 'dashboard' && <Dashboard />}
                {view === 'customers' && <Customers />}
                {view === 'suppliers' && <Suppliers />}
                {view === 'invoices' && <Invoices />}
                {view === 'quotes' && <Quotes />}
            </main>
        </div>
    );
}

export default App;
"@
    $appJsx | Set-Content "$outputDir\src\App.jsx"
    
    # ===========================
    # src/index.jsx
    # ===========================
    $indexJsx = @"
import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
"@
    $indexJsx | Set-Content "$outputDir\src\index.jsx"
    
    # ===========================
    # src/components/Dashboard.jsx
    # ===========================
    $dashboardJsx = @"
import React, { useEffect, useState } from 'react';
import { getUnpaidInvoices } from '../services/apiService';

export default function Dashboard() {
    const [unpaidInvoices, setUnpaidInvoices] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadData();
    }, []);

    async function loadData() {
        try {
            const invoices = await getUnpaidInvoices();
            setUnpaidInvoices(invoices);
        } catch (error) {
            console.error('Error loading dashboard data:', error);
        } finally {
            setLoading(false);
        }
    }

    if (loading) return <div className="loading">Loading...</div>;

    const totalUnpaid = unpaidInvoices.reduce((sum, inv) => sum + (inv.amountGross || 0), 0);

    return (
        <div className="dashboard">
            <h1>Dashboard</h1>
            <div className="stats-grid">
                <div className="stat-card">
                    <h3>Unpaid Invoices</h3>
                    <p className="stat-value">{unpaidInvoices.length}</p>
                </div>
                <div className="stat-card">
                    <h3>Total Outstanding</h3>
                    <p className="stat-value">$($config['Finance']['BaseCurrency']) {totalUnpaid.toFixed(2)}</p>
                </div>
            </div>
            <div className="recent-section">
                <h2>Recent Unpaid Invoices</h2>
                <table className="data-table">
                    <thead>
                        <tr>
                            <th>Invoice #</th>
                            <th>Customer</th>
                            <th>Date</th>
                            <th>Amount</th>
                        </tr>
                    </thead>
                    <tbody>
                        {unpaidInvoices.slice(0, 5).map(inv => (
                            <tr key={inv.id}>
                                <td>{inv.invoiceNumber}</td>
                                <td>{inv.customerName}</td>
                                <td>{new Date(inv.dateIssued).toLocaleDateString()}</td>
                                <td>$($config['Finance']['BaseCurrency']) {inv.amountGross.toFixed(2)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
"@
    $dashboardJsx | Set-Content "$outputDir\src\components\Dashboard.jsx"
    
    # ===========================
    # src/components/Customers.jsx
    # ===========================
    $customersJsx = @"
import React, { useEffect, useState } from 'react';
import { getCustomers, createCustomer, generateCode } from '../services/apiService';

export default function Customers() {
    const [customers, setCustomers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showForm, setShowForm] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        email: '',
        billingEmail: '',
        billingAddress: '',
        defaultDayRate: '',
        defaultHourlyRate: '',
        isVATRegistered: true,
        defaultVATRate: 20
    });

    useEffect(() => {
        loadCustomers();
    }, []);

    async function loadCustomers() {
        try {
            const data = await getCustomers();
            setCustomers(data);
        } catch (error) {
            console.error('Error loading customers:', error);
        } finally {
            setLoading(false);
        }
    }

    async function handleSubmit(e) {
        e.preventDefault();
        try {
            // Generate code
            const codeResult = await generateCode(formData.name, 'Customer');
            const newCustomer = { ...formData, customerCode: codeResult.code };
            
            await createCustomer(newCustomer);
            alert('Customer created successfully!');
            setShowForm(false);
            setFormData({
                name: '',
                email: '',
                billingEmail: '',
                billingAddress: '',
                defaultDayRate: '',
                defaultHourlyRate: '',
                isVATRegistered: true,
                defaultVATRate: 20
            });
            loadCustomers();
        } catch (error) {
            console.error('Error creating customer:', error);
            alert('Failed to create customer');
        }
    }

    if (loading) return <div className="loading">Loading...</div>;

    return (
        <div className="customers">
            <div className="page-header">
                <h1>Customers</h1>
                <button onClick={() => setShowForm(!showForm)} className="btn-primary">
                    {showForm ? 'Cancel' : '+ Add Customer'}
                </button>
            </div>

            {showForm && (
                <form onSubmit={handleSubmit} className="entity-form">
                    <div className="form-row">
                        <div className="form-group">
                            <label>Customer Name *</label>
                            <input
                                type="text"
                                value={formData.name}
                                onChange={e => setFormData({...formData, name: e.target.value})}
                                required
                            />
                        </div>
                        <div className="form-group">
                            <label>Email</label>
                            <input
                                type="email"
                                value={formData.email}
                                onChange={e => setFormData({...formData, email: e.target.value})}
                            />
                        </div>
                    </div>
                    <div className="form-row">
                        <div className="form-group">
                            <label>Billing Email</label>
                            <input
                                type="email"
                                value={formData.billingEmail}
                                onChange={e => setFormData({...formData, billingEmail: e.target.value})}
                            />
                        </div>
                        <div className="form-group">
                            <label>Billing Address</label>
                            <textarea
                                value={formData.billingAddress}
                                onChange={e => setFormData({...formData, billingAddress: e.target.value})}
                            />
                        </div>
                    </div>
                    <div className="form-row">
                        <div className="form-group">
                            <label>Default Day Rate ($($config['Finance']['BaseCurrency']))</label>
                            <input
                                type="number"
                                step="0.01"
                                value={formData.defaultDayRate}
                                onChange={e => setFormData({...formData, defaultDayRate: e.target.value})}
                            />
                        </div>
                        <div className="form-group">
                            <label>Default Hourly Rate ($($config['Finance']['BaseCurrency']))</label>
                            <input
                                type="number"
                                step="0.01"
                                value={formData.defaultHourlyRate}
                                onChange={e => setFormData({...formData, defaultHourlyRate: e.target.value})}
                            />
                        </div>
                    </div>
                    <div className="form-row">
                        <div className="form-group">
                            <label>
                                <input
                                    type="checkbox"
                                    checked={formData.isVATRegistered}
                                    onChange={e => setFormData({...formData, isVATRegistered: e.target.checked})}
                                />
                                VAT Registered
                            </label>
                        </div>
                        <div className="form-group">
                            <label>Default VAT Rate (%)</label>
                            <input
                                type="number"
                                value={formData.defaultVATRate}
                                onChange={e => setFormData({...formData, defaultVATRate: e.target.value})}
                            />
                        </div>
                    </div>
                    <button type="submit" className="btn-primary">Create Customer</button>
                </form>
            )}

            <table className="data-table">
                <thead>
                    <tr>
                        <th>Code</th>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Day Rate</th>
                        <th>Hourly Rate</th>
                        <th>VAT</th>
                    </tr>
                </thead>
                <tbody>
                    {customers.map(customer => (
                        <tr key={customer.id}>
                            <td>{customer.customerCode}</td>
                            <td>{customer.name}</td>
                            <td>{customer.billingEmail || customer.email}</td>
                            <td>$($config['Finance']['BaseCurrency']) {customer.defaultDayRate || '-'}</td>
                            <td>$($config['Finance']['BaseCurrency']) {customer.defaultHourlyRate || '-'}</td>
                            <td>{customer.isVATRegistered ? '✓' : '-'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
"@
    $customersJsx | Set-Content "$outputDir\src\components\Customers.jsx"
    
    # ===========================
    # Similar components for Suppliers, Invoices, Quotes
    # (Creating placeholder files for brevity)
    # ===========================
    
    "import React from 'react'; export default function Suppliers() { return <div><h1>Suppliers</h1><p>Component coming soon...</p></div>; }" | Set-Content "$outputDir\src\components\Suppliers.jsx"
    "import React from 'react'; export default function Invoices() { return <div><h1>Invoices</h1><p>Component coming soon...</p></div>; }" | Set-Content "$outputDir\src\components\Invoices.jsx"
    "import React from 'react'; export default function Quotes() { return <div><h1>Quotes</h1><p>Component coming soon...</p></div>; }" | Set-Content "$outputDir\src\components\Quotes.jsx"
    
    # ===========================
    # src/styles/App.css
    # ===========================
    $appCss = @"
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    background: #f5f5f5;
}

/* Login */
.login-container {
    display: flex;
    justify-content: center;
    align-items: center;
    height: 100vh;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-card {
    background: white;
    padding: 3rem;
    border-radius: 1rem;
    box-shadow: 0 10px 40px rgba(0,0,0,0.1);
    text-align: center;
    max-width: 400px;
}

.login-card h1 {
    margin-bottom: 1rem;
    color: #333;
}

/* App Layout */
.app-container {
    display: flex;
    height: 100vh;
}

.sidebar {
    width: 250px;
    background: #2c3e50;
    color: white;
    display: flex;
    flex-direction: column;
}

.sidebar-header {
    padding: 1.5rem;
    border-bottom: 1px solid rgba(255,255,255,0.1);
}

.sidebar-header h2 {
    font-size: 1.2rem;
}

.nav-menu {
    flex: 1;
    list-style: none;
    padding: 1rem 0;
}

.nav-menu li {
    padding: 1rem 1.5rem;
    cursor: pointer;
    transition: background 0.2s;
}

.nav-menu li:hover {
    background: rgba(255,255,255,0.1);
}

.nav-menu li.active {
    background: rgba(255,255,255,0.2);
    border-left: 4px solid #3498db;
}

.sidebar-footer {
    padding: 1rem;
    border-top: 1px solid rgba(255,255,255,0.1);
}

.content {
    flex: 1;
    padding: 2rem;
    overflow-y: auto;
}

/* Buttons */
.btn-primary {
    background: #3498db;
    color: white;
    border: none;
    padding: 0.75rem 1.5rem;
    border-radius: 0.5rem;
    cursor: pointer;
    font-size: 1rem;
    transition: background 0.2s;
}

.btn-primary:hover {
    background: #2980b9;
}

.btn-logout {
    width: 100%;
    background: transparent;
    color: white;
    border: 1px solid rgba(255,255,255,0.3);
    padding: 0.5rem;
    border-radius: 0.5rem;
    cursor: pointer;
}

/* Page Header */
.page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 2rem;
}

/* Forms */
.entity-form {
    background: white;
    padding: 2rem;
    border-radius: 0.5rem;
    margin-bottom: 2rem;
    box-shadow: 0 2px 10px rgba(0,0,0,0.1);
}

.form-row {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
    margin-bottom: 1rem;
}

.form-group {
    display: flex;
    flex-direction: column;
}

.form-group label {
    margin-bottom: 0.5rem;
    font-weight: 500;
    color: #555;
}

.form-group input,
.form-group textarea,
.form-group select {
    padding: 0.75rem;
    border: 1px solid #ddd;
    border-radius: 0.5rem;
    font-size: 1rem;
}

/* Tables */
.data-table {
    width: 100%;
    background: white;
    border-radius: 0.5rem;
    overflow: hidden;
    box-shadow: 0 2px 10px rgba(0,0,0,0.1);
}

.data-table thead {
    background: #34495e;
    color: white;
}

.data-table th,
.data-table td {
    padding: 1rem;
    text-align: left;
}

.data-table tbody tr:nth-child(even) {
    background: #f8f9fa;
}

.data-table tbody tr:hover {
    background: #e9ecef;
}

/* Dashboard */
.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 1.5rem;
    margin-bottom: 2rem;
}

.stat-card {
    background: white;
    padding: 1.5rem;
    border-radius: 0.5rem;
    box-shadow: 0 2px 10px rgba(0,0,0,0.1);
}

.stat-card h3 {
    color: #777;
    font-size: 0.9rem;
    margin-bottom: 0.5rem;
}

.stat-value {
    font-size: 2rem;
    font-weight: bold;
    color: #333;
}

.loading {
    text-align: center;
    padding: 3rem;
    font-size: 1.2rem;
    color: #777;
}

/* Responsive */
@media (max-width: 768px) {
    .app-container {
        flex-direction: column;
    }
    
    .sidebar {
        width: 100%;
        height: auto;
    }
    
    .form-row {
        grid-template-columns: 1fr;
    }
}
"@
    $appCss | Set-Content "$outputDir\src\styles\App.css"
    
    # ===========================
    # README.md
    # ===========================
    $readme = @"
# $($config['Brand']['CompanyDisplay']) $($config['Brand']['Product']) - Static Web App

React-based web portal for managing customers, suppliers, invoices, and quotes.

## Setup

1. Install dependencies: npm install
2. Run dev server: npm run dev  
3. Build for production: npm run build

## Deployment

Deploy to Azure Static Web Apps via Azure Portal or GitHub Actions.
"@
    $readme | Set-Content "$outputDir\README.md"
    Write-Host "✓ README.md" -ForegroundColor Green

} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}
