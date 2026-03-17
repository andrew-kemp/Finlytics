import React, { useEffect, useState, useRef } from 'react';
import TrivialBenefitModal from './TrivialBenefitModal';
import {
    getInvoices,
    getExpenses,
    getCompanyAggregates,
    getYtdAggregates,
    getCompanySettings,
    getVatReturns,
    getDlaEntries
} from '../services/apiService';

export default function Dashboard({ onNavigate }) {
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false); // subtle indicator for period changes
    const [period, setPeriod] = useState('quarter'); // week, month, quarter, ytd, last12, all, specific
    const [selectedYear, setSelectedYear] = useState(null);   // null = use period buttons
    const [selectedQuarter, setSelectedQuarter] = useState(null); // 0-3 or null = full year
    const [metrics, setMetrics] = useState(null);
    const [companySettings, setCompanySettings] = useState(null);
    const [showNonCtModal, setShowNonCtModal] = useState(false);
    const [showCtModal, setShowCtModal] = useState(false);
    const [showQuickMenu, setShowQuickMenu] = useState(false);
    const [showTrivialBenefit, setShowTrivialBenefit] = useState(false);
    const [directors, setDirectors] = useState([]);
    const quickMenuRef = useRef(null);

    // Close dropdown on outside click
    useEffect(() => {
        const handler = (e) => { if (quickMenuRef.current && !quickMenuRef.current.contains(e.target)) setShowQuickMenu(false); };
        document.addEventListener('mousedown', handler);
        return () => document.removeEventListener('mousedown', handler);
    }, []);

    useEffect(() => {
        loadData();
    }, [period, selectedYear, selectedQuarter]);

    const getDateRange = (settings) => {
        const now = new Date();
        let startDate, endDate = now;

        switch (period) {
            case 'week':{
                startDate = new Date(now);
                const day = startDate.getDay();
                const diff = startDate.getDate() - day + (day === 0 ? -6 : 1);
                startDate.setDate(diff);
                startDate.setHours(0, 0, 0, 0);
                break;
            }
            case 'month':
                startDate = new Date(now.getFullYear(), now.getMonth(), 1);
                break;
            case 'quarter':{
                const qMonth = Math.floor(now.getMonth() / 3) * 3;
                startDate = new Date(now.getFullYear(), qMonth, 1);
                break;
            }
            case 'ytd':{
                // Use company inception date if it's more recent than UK tax year start (April 6)
                const taxYearStart = new Date(now.getFullYear(), 3, 6);
                if (now < taxYearStart) taxYearStart.setFullYear(now.getFullYear() - 1);
                const inceptionRaw = settings?.companyInceptionDate || settings?.incorporationDate;
                const inceptionDate = inceptionRaw ? new Date(inceptionRaw) : null;
                startDate = (inceptionDate && inceptionDate > taxYearStart) ? inceptionDate : taxYearStart;
                break;
            }
            case 'last12':{
                startDate = new Date(now);
                startDate.setFullYear(startDate.getFullYear() - 1);
                startDate.setHours(0, 0, 0, 0);
                break;
            }
            case 'all':
                startDate = new Date(2000, 0, 1);
                break;
            case 'specific':{
                if (selectedYear !== null) {
                    if (selectedQuarter !== null) {
                        // Specific Q e.g. Q1 = Jan-Mar
                        const qStart = selectedQuarter * 3;
                        startDate = new Date(selectedYear, qStart, 1);
                        endDate = new Date(selectedYear, qStart + 3, 0, 23, 59, 59);
                    } else {
                        // Full year
                        startDate = new Date(selectedYear, 0, 1);
                        endDate = new Date(selectedYear, 11, 31, 23, 59, 59);
                    }
                } else {
                    startDate = new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 1);
                }
                break;
            }
            default:
                startDate = new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 1);
        }

        return { startDate, endDate };
    };

    const filterByDateRange = (items, dateField, settings) => {
        const { startDate, endDate } = getDateRange(settings);
        return items.filter(item => {
            if (!item[dateField]) return false;
            const itemDate = new Date(item[dateField]);
            return itemDate >= startDate && itemDate <= endDate;
        });
    };

    const calculateCorporationTax = (profit) => {
        if (profit <= 0) return 0;
        const lowerLimit = 50000;
        const upperLimit = 250000;
        const smallRate = 0.19;
        const mainRate = 0.25;

        if (profit <= lowerLimit) {
            return profit * smallRate;
        } else if (profit >= upperLimit) {
            return profit * mainRate;
        } else {
            const marginalRelief = ((upperLimit - profit) / upperLimit) * (mainRate - smallRate) * profit;
            return (profit * mainRate) - marginalRelief;
        }
    };

    const getCurrentPeriodKey = () => {
        const now = new Date();
        return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
    };

    async function loadData() {
        try {
            // First load: show full blank spinner. Subsequent reloads: keep showing data
            if (metrics) { setRefreshing(true); } else { setLoading(true); }
            
            // Fire all fetches in parallel — no sequential waterfalls
            const [invoices, expenses, companyAggregates, settings, filedReturns, dlaEntries, ytdAggregates] = await Promise.all([
                getInvoices(),
                getExpenses(),
                getCompanyAggregates(getCurrentPeriodKey()).catch(() => ({})),
                getCompanySettings().catch(() => null),
                getVatReturns().catch(() => []),
                getDlaEntries().catch(() => []),
                getYtdAggregates().catch(() => ({}))
            ]);

            setCompanySettings(settings);

            // Extract directors list for TrivialBenefitModal
            if (settings?.directors) {
                setDirectors(settings.directors.split(',').map(d => d.trim()).filter(Boolean));
            } else if (settings?.directorName) {
                setDirectors([settings.directorName]);
            }
            const dlaOwedToDirector = dlaEntries
                .filter(e => e.direction === 'OwedToDirector')
                .reduce((sum, e) => sum + (e.remainingBalance ?? e.amountGross ?? 0), 0);
            const dlaOwedToCompany = dlaEntries
                .filter(e => e.direction === 'OwedToCompany')
                .reduce((sum, e) => sum + (e.remainingBalance ?? e.amountGross ?? 0), 0);
            // HMRC VAT blocking: only specific categories have blocked input tax.
            // - Client entertainment (SI 1992/3222 Business Entertainment Regulations): blocked
            // - Trivial benefits (s.323A ITEPA): CT-deductible but VAT is not reclaimable
            // - NonCT tagging alone does NOT block VAT — those are separate HMRC rules
            const isVatBlocked = (item) => {
                const cat = (item.category || '').toLowerCase();
                return cat.includes('entertainment') ||
                       cat === 'trivial benefit' ||
                       item.isTrivialBenefit === true;
            };
            // DLA input VAT (OwedToDirector = director paid company expense = reclaimable)
            // Excludes entertainment/NonCT entries per HMRC Business Entertainment Regulations
            const dlaVatReclaimable = dlaEntries
                .filter(e => e.direction === 'OwedToDirector' && !isVatBlocked(e))
                .reduce((sum, e) => sum + (e.vatAmount || 0), 0);
            // positive = director owes company; negative = company owes director
            const dlaNetCalc = dlaOwedToCompany - dlaOwedToDirector;
            const allInvoiceVat = invoices.filter(inv => inv.status === 'Paid').reduce((sum, inv) => sum + (inv.vatAmount || 0), 0);
            const allExpenseVat = expenses
                .filter(exp => !exp.isDLA && !isVatBlocked(exp))
                .reduce((sum, exp) => sum + (exp.vatAmount || 0), 0);
            const totalBlockedVat =
                expenses.filter(exp => !exp.isDLA && isVatBlocked(exp)).reduce((sum, e) => sum + (e.vatAmount || 0), 0) +
                dlaEntries.filter(e => e.direction === 'OwedToDirector' && isVatBlocked(e))
                    .reduce((sum, e) => sum + (e.vatAmount || 0), 0);
            const filedVatNet = filedReturns.reduce((sum, fr) => sum + (fr.vatOwed || 0), 0);
            // Unfiled VAT = (sales VAT - expenses VAT - DLA input VAT) - already filed
            const unfiledVatBalance = (allInvoiceVat - allExpenseVat - dlaVatReclaimable) - filedVatNet;

            const periodInvoices = filterByDateRange(invoices, 'dateIssued', settings);
            const periodExpenses = filterByDateRange(expenses.filter(e => !e.isDLA).map(e => ({
                ...e,
                entryDate: e.entryDate || e.datePaid
            })), 'entryDate', settings);
            const periodDlaEntries = filterByDateRange(
                dlaEntries.filter(e => e.direction === 'OwedToDirector'),
                'entryDate', settings
            );

            // Income: count invoices that are Paid OR Sent (outstanding) in period
            const paidInvoices = periodInvoices.filter(inv => inv.status === 'Paid');
            const income = paidInvoices.reduce((sum, inv) => sum + (inv.amountGross || 0), 0);
            const incomeNet = paidInvoices.reduce((sum, inv) => sum + (inv.amountNet || 0), 0);
            const incomeVAT = paidInvoices.reduce((sum, inv) => sum + (inv.vatAmount || 0), 0);
            // All invoices in period (any status except Draft) for outstanding count
            const billedInvoices = periodInvoices.filter(inv => inv.status !== 'Draft');
            const billedTotal = billedInvoices.reduce((sum, inv) => sum + (inv.amountGross || 0), 0);

            const expenseGross = periodExpenses.reduce((sum, exp) => sum + (exp.amountGross || 0), 0);
            const expenseNet = periodExpenses.reduce((sum, exp) => sum + (exp.amountNet || 0), 0);
            const expenseVAT = periodExpenses
                .filter(exp => !isVatBlocked(exp))
                .reduce((sum, exp) => sum + (exp.vatAmount || 0), 0);
            const periodDlaVat = periodDlaEntries
                .filter(e => !isVatBlocked(e))
                .reduce((sum, e) => sum + (e.vatAmount || 0), 0);

            // ── CT estimate — always based on full financial year from inception ──
            // Income & expenses: filtered from inception date (or April 6 if not set).
            // DLA: ALL OwedToDirector entries are deducted with NO date filter.
            // This matches the DLA page's own "CT-deductible (all-time net)" figure.
            // A company's DLA are all incurred within its first accounting period,
            // and pre-incorporation startup costs are deductible in year 1 (s.61 CTA 2009).
            const ctYtdStart = (() => {
                const raw = settings?.companyInceptionDate || settings?.incorporationDate;
                if (raw) return new Date(raw);
                const n = new Date();
                const ys = new Date(n.getFullYear(), 3, 6); // 6 April
                if (n < ys) ys.setFullYear(n.getFullYear() - 1);
                return ys;
            })();

            const ctYtdPaidInvoices = invoices.filter(inv =>
                inv.status === 'Paid' && inv.dateIssued && new Date(inv.dateIssued) >= ctYtdStart);
            const ctYtdExpenses = expenses.filter(exp =>
                !exp.isDLA && exp.entryDate && new Date(exp.entryDate) >= ctYtdStart);

            const ctIncomeNet   = ctYtdPaidInvoices.reduce((sum, inv) => sum + (inv.amountNet || 0), 0);
            const ctAllowableExpenseNet = ctYtdExpenses
                .filter(exp => exp.ctTag !== 'NonCT')
                .reduce((sum, exp) => sum + (exp.amountNet || 0), 0);
            const nonCtExpenseItems = ctYtdExpenses.filter(exp => exp.ctTag === 'NonCT');
            const nonCtExpenseGross = nonCtExpenseItems.reduce((sum, exp) => sum + (exp.amountGross || 0), 0);
            const nonCtDlaItems = dlaEntries
                .filter(e => e.direction === 'OwedToDirector' && e.ctTag === 'NonCT');
            const nonCtDlaGross = nonCtDlaItems.reduce((sum, e) => sum + (e.amountGross || 0), 0);

            // DLA: deduct ALL OwedToDirector entries (no date filter) — matches DLA page CT figure
            const ctAllowableDlaNet = dlaEntries
                .filter(e => e.direction === 'OwedToDirector' && e.ctTag !== 'NonCT')
                .reduce((sum, e) => sum + (e.amountNet || 0), 0);

            const vatBalance = incomeVAT - expenseVAT - dlaVatReclaimable;
            const tradingProfit = ctIncomeNet - ctAllowableExpenseNet - ctAllowableDlaNet;

            const salary = ytdAggregates?.salaryGross || 0;
            const employerNI = ytdAggregates?.employerNI || 0;
            const employeeNI = ytdAggregates?.employeeNI || 0;
            const profitBeforeTax = tradingProfit - salary - employerNI;
            const corpTaxEstimate = calculateCorporationTax(profitBeforeTax);
            const corpTaxPaid = ytdAggregates?.corpTaxPaid || 0;
            const corpTaxDue = Math.max(0, corpTaxEstimate - corpTaxPaid);

            const currentBalance = income - expenseGross - salary - (ytdAggregates?.payeRemitted || 0) - 
                                   employeeNI - employerNI - corpTaxPaid - (ytdAggregates?.dividendsPaid || 0);

            setMetrics({
                income, incomeNet, incomeVAT,
                billedTotal,
                expenses: expenseGross, expenseNet, expenseVAT, nonCtExpenseGross, nonCtExpenseItems,
                nonCtDlaItems, nonCtDlaGross, totalBlockedVat,
                vatIn: incomeVAT, vatOut: expenseVAT + periodDlaVat, vatBalance,
                allVatIn: allInvoiceVat,
                allVatOut: allExpenseVat + dlaVatReclaimable,
                filedVatNet,
                salary, employeeNI, employerNI,
                dividendsDeclared: ytdAggregates?.dividendsDeclared || 0,
                dividendsPaid: ytdAggregates?.dividendsPaid || 0,
                dlaNet: dlaNetCalc,
                dlaOwedToDirector,
                dlaOwedToCompany,
                dlaVatReclaimable,
                ctIncomeNet,
                ctAllowableExpenseNet,
                ctAllowableDlaNet,
                currentBalance, tradingProfit, profitBeforeTax,
                corpTaxEstimate, corpTaxPaid, corpTaxDue,
                unpaidInvoices: invoices.filter(inv => inv.status !== 'Paid' && inv.status !== 'Draft').length,
                unpaidAmount: invoices.filter(inv => inv.status !== 'Paid' && inv.status !== 'Draft')
                    .reduce((sum, inv) => sum + (inv.amountGross || 0), 0),
                psaApproved: settings?.psaApproved || false,
                psaExpenses: periodExpenses
                    .filter(exp => exp.category === 'Staff Entertainment (PSA)')
                    .reduce((sum, exp) => sum + (exp.amountGross || 0), 0),
                unfiledVatBalance
            });

        } catch (error) {
            console.error('Error loading dashboard data:', error);
        } finally {
            setLoading(false);
            setRefreshing(false);
        }
    }

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('en-GB', {
            style: 'currency',
            currency: 'GBP'
        }).format(amount || 0);
    };

    const getPeriodLabel = () => {
        const now = new Date();
        const qNames = ['Q1 (Jan–Mar)', 'Q2 (Apr–Jun)', 'Q3 (Jul–Sep)', 'Q4 (Oct–Dec)'];
        const currentQ = qNames[Math.floor(now.getMonth() / 3)];
        if (period === 'specific' && selectedYear !== null) {
            if (selectedQuarter !== null) return `${qNames[selectedQuarter]} ${selectedYear}`;
            return `Full Year ${selectedYear}`;
        }
        switch (period) {
            case 'week': return 'This Week';
            case 'month': return 'This Month';
            case 'quarter': return `This Quarter — ${currentQ} ${now.getFullYear()}`;
            case 'ytd': return 'This Tax Year';
            case 'last12': return 'Last 12 Months';
            case 'all': return 'All Time';
            default: return 'This Quarter';
        }
    };

    // Build year options from inception to current year
    const getYearOptions = () => {
        const now = new Date();
        const currentYear = now.getFullYear();
        const inceptionRaw = companySettings?.companyInceptionDate || companySettings?.incorporationDate;
        const inceptionYear = inceptionRaw ? new Date(inceptionRaw).getFullYear() : currentYear;
        const years = [];
        for (let y = currentYear; y >= inceptionYear; y--) years.push(y);
        return years;
    };

    if (loading) return (
        <div className="loading-container">
            <div className="spinner"></div>
            <div className="loading-text">Loading dashboard...</div>
        </div>
    );

    if (!metrics) return null;

    return (
        <>
        <div className="dashboard">
            <div className="page-header">
                <h1>Dashboard {refreshing && <span style={{ fontSize: '0.75rem', fontWeight: 400, color: '#94a3b8', marginLeft: 8 }}>Refreshing…</span>}</h1>
                <div className="quick-actions">
                    {/* + New Entry dropdown */}
                    <div className="quick-add-menu" ref={quickMenuRef}>
                        <button
                            className="btn-primary"
                            onClick={() => setShowQuickMenu(v => !v)}
                        >
                            + New Entry <span style={{ fontSize: '0.75em', opacity: 0.8 }}>▾</span>
                        </button>
                        {showQuickMenu && (
                            <div className="quick-add-dropdown">
                                <button onClick={() => { setShowQuickMenu(false); onNavigate && onNavigate('expenses', { openNew: true }); }}>
                                    💳 Expense
                                </button>
                                <button onClick={() => { setShowQuickMenu(false); onNavigate && onNavigate('dla', { openNew: true }); }}>
                                    🏦 DLA Entry
                                </button>
                                <button onClick={() => { setShowQuickMenu(false); onNavigate && onNavigate('mileage', { openNew: true }); }}>
                                    🚗 Mileage Trip
                                </button>
                                <button onClick={() => { setShowQuickMenu(false); setShowTrivialBenefit(true); }}>
                                    🎁 Trivial Benefit
                                </button>
                            </div>
                        )}
                    </div>
                    <button onClick={() => onNavigate && onNavigate('invoices')} className="btn-secondary">
                        + New Invoice
                    </button>
                    <button onClick={() => onNavigate && onNavigate('companyledger')} className="btn-secondary">
                        + Ledger Entry
                    </button>
                </div>
            </div>

            <div className="dashboard-controls">
                <div className="period-selector">
                    {['week','month','quarter','ytd','last12','all'].map(p => (
                        <button key={p}
                            className={`period-btn ${period === p && period !== 'specific' ? 'active' : ''}`}
                            onClick={() => { setPeriod(p); setSelectedYear(null); setSelectedQuarter(null); }}
                        >
                            {{ week:'This Week', month:'This Month', quarter:'This Quarter', ytd:'This Tax Year', last12:'Last 12 Months', all:'All Time' }[p]}
                        </button>
                    ))}
                </div>
                {/* Specific year / quarter row */}
                <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                    <span style={{ fontSize: '0.82rem', color: '#6c757d', lineHeight: '32px' }}>Specific period:</span>
                    <select
                        value={period === 'specific' ? (selectedYear ?? '') : ''}
                        onChange={e => {
                            const yr = e.target.value ? parseInt(e.target.value) : null;
                            setSelectedYear(yr);
                            if (yr) { setPeriod('specific'); } else { setPeriod('quarter'); setSelectedQuarter(null); }
                        }}
                        style={{ padding: '4px 8px', borderRadius: 6, border: '1px solid #dee2e6', fontSize: '0.85rem', cursor: 'pointer' }}
                    >
                        <option value="">Select year…</option>
                        {getYearOptions().map(y => <option key={y} value={y}>{y}</option>)}
                    </select>
                    {period === 'specific' && selectedYear && (
                        <select
                            value={selectedQuarter !== null ? selectedQuarter : ''}
                            onChange={e => setSelectedQuarter(e.target.value !== '' ? parseInt(e.target.value) : null)}
                            style={{ padding: '4px 8px', borderRadius: 6, border: '1px solid #dee2e6', fontSize: '0.85rem', cursor: 'pointer' }}
                        >
                            <option value="">Full year</option>
                            <option value="0">Q1 (Jan–Mar)</option>
                            <option value="1">Q2 (Apr–Jun)</option>
                            <option value="2">Q3 (Jul–Sep)</option>
                            <option value="3">Q4 (Oct–Dec)</option>
                        </select>
                    )}
                    {period === 'specific' && (
                        <button className="btn-secondary" style={{ fontSize: '0.78rem', padding: '3px 10px' }}
                            onClick={() => { setPeriod('quarter'); setSelectedYear(null); setSelectedQuarter(null); }}>
                            × Clear
                        </button>
                    )}
                </div>
            </div>

            <h2 className="dashboard-section-title">{getPeriodLabel()} Overview</h2>

            <div className="metrics-grid">
                <div className="metric-card income">
                    <div className="metric-icon">💰</div>
                    <div className="metric-content">
                        <div className="metric-label">Income (Paid)</div>
                        <div className="metric-value">{formatCurrency(metrics.income)}</div>
                        <div className="metric-detail">Net: {formatCurrency(metrics.incomeNet)} | Billed: {formatCurrency(metrics.billedTotal)}</div>
                    </div>
                </div>

                <div className="metric-card expenses">
                    <div className="metric-icon">📊</div>
                    <div className="metric-content">
                        <div className="metric-label">Expenses</div>
                        <div className="metric-value">{formatCurrency(metrics.expenses)}</div>
                        <div className="metric-detail">Net: {formatCurrency(metrics.expenseNet)}</div>
                    </div>
                </div>

                <div className="metric-card vat" style={{ cursor: 'pointer' }} onClick={() => onNavigate && onNavigate('vatreturns')}>
                    <div className="metric-icon">🧾</div>
                    <div className="metric-content">
                        <div className="metric-label">VAT — Unfiled Balance</div>
                        <div className={`metric-value ${(metrics.unfiledVatBalance || 0) >= 0 ? 'positive' : 'negative'}`}>
                            {formatCurrency(metrics.unfiledVatBalance || 0)}
                        </div>
                        <div className="metric-detail">
                            Filed: {formatCurrency(metrics.filedVatNet || 0)} | All-time In: {formatCurrency(metrics.allVatIn || 0)} | Out: {formatCurrency(metrics.allVatOut || 0)}
                        </div>
                    </div>
                </div>

                <div className="metric-card salary">
                    <div className="metric-icon">👤</div>
                    <div className="metric-content">
                        <div className="metric-label">Salary & NI</div>
                        <div className="metric-value">{formatCurrency(metrics.salary)}</div>
                        <div className="metric-detail">
                            Emp NI: {formatCurrency(metrics.employeeNI)} | Er NI: {formatCurrency(metrics.employerNI)}
                        </div>
                    </div>
                </div>
                {metrics.psaApproved && (
                    <div className="metric-card psa">
                        <div className="metric-icon">📋</div>
                        <div className="metric-content">
                            <div className="metric-label">PSA Expenses</div>
                            <div className="metric-value">{formatCurrency(metrics.psaExpenses)}</div>
                            <div className="metric-detail">Staff entertainment via HMRC PSA</div>
                        </div>
                    </div>
                )}
                <div className="metric-card corp-tax" style={{ cursor: 'pointer' }} onClick={() => setShowCtModal(true)}>
                    <div className="metric-icon">🏛️</div>
                    <div className="metric-content">
                        <div className="metric-label">Corporation Tax</div>
                        <div className="metric-value">{formatCurrency(metrics.corpTaxEstimate)}</div>
                        <div className="metric-detail">
                            Due: {formatCurrency(metrics.corpTaxDue)} <span style={{fontSize:'0.75em', opacity:0.6}}>· tap for breakdown</span>
                        </div>
                    </div>
                </div>

                <div className="metric-card dividends">
                    <div className="metric-icon">💎</div>
                    <div className="metric-content">
                        <div className="metric-label">Dividends</div>
                        <div className="metric-value">{formatCurrency(metrics.dividendsDeclared)}</div>
                        <div className="metric-detail">Paid: {formatCurrency(metrics.dividendsPaid)}</div>
                    </div>
                </div>

                <div className="metric-card dla" style={{ cursor: 'pointer' }} onClick={() => onNavigate && onNavigate('dla')}>
                    <div className="metric-icon">🔄</div>
                    <div className="metric-content">
                        <div className="metric-label">Directors Loan</div>
                        <div className={`metric-value ${metrics.dlaNet <= 0 ? 'positive' : 'negative'}`}>
                            {formatCurrency(Math.abs(metrics.dlaNet))}
                        </div>
                        <div className="metric-detail">
                            {metrics.dlaNet > 0
                                ? `Director owes company`
                                : metrics.dlaNet < 0
                                    ? `Company owes director`
                                    : 'Balance clear'}
                        </div>
                        {(metrics.dlaOwedToDirector > 0 || metrics.dlaOwedToCompany > 0) && (
                            <div className="metric-detail" style={{ marginTop: 2 }}>
                                ↑ {formatCurrency(metrics.dlaOwedToCompany)} | ↓ {formatCurrency(metrics.dlaOwedToDirector)}
                            </div>
                        )}
                        {metrics.dlaVatReclaimable > 0 && (
                            <div className="metric-detail" style={{ marginTop: 2, color: '#28a745' }}>
                                VAT reclaimable: {formatCurrency(metrics.dlaVatReclaimable)}
                            </div>
                        )}
                    </div>
                </div>

                <div className="metric-card balance">
                    <div className="metric-icon">🏦</div>
                    <div className="metric-content">
                        <div className="metric-label">Estimated Balance</div>
                        <div className={`metric-value ${metrics.currentBalance >= 0 ? 'positive' : 'negative'}`}>
                            {formatCurrency(metrics.currentBalance)}
                        </div>
                        <div className="metric-detail">Trading: {formatCurrency(metrics.tradingProfit)}</div>
                    </div>
                </div>
            </div>

            <div className="dashboard-info">
                <div className="info-card">
                    <h3>⚠️ Outstanding Items</h3>
                    <div className="info-content">
                        <div className="info-row">
                            <span>Unpaid Invoices:</span>
                            <span><strong>{metrics.unpaidInvoices}</strong> ({formatCurrency(metrics.unpaidAmount)})</span>
                        </div>
                        <div className="info-row">
                            <span>VAT Unfiled (all-time):</span>
                            <span className={(metrics.unfiledVatBalance || 0) >= 0 ? 'text-danger' : 'text-success'}>
                                <strong>{formatCurrency(Math.abs(metrics.unfiledVatBalance || 0))}</strong>
                                {(metrics.unfiledVatBalance || 0) < 0 && ' (HMRC owes you)'}
                            </span>
                        </div>
                        <div className="info-row">
                            <span>Corporation Tax Due:</span>
                            <span className="text-danger">
                                <strong>{formatCurrency(metrics.corpTaxDue)}</strong>
                            </span>
                        </div>
                    </div>
                </div>

                <div className="info-card">
                    <h3>💡 Tax Calculation <small style={{fontWeight:400, fontSize:'0.75rem', opacity:0.6}}>(financial year to date)</small></h3>
                    <div className="info-content">
                        <div className="info-row">
                            <span>Income (net, excl. VAT):</span>
                            <span>{formatCurrency(metrics.ctIncomeNet)}</span>
                        </div>
                        <div className="info-row">
                            <span>Less: CT-allowable expenses:</span>
                            <span>-{formatCurrency(metrics.ctAllowableExpenseNet)}</span>
                        </div>
                        {metrics.ctAllowableDlaNet > 0 && (
                            <div className="info-row" style={{color: '#16a34a', fontSize: '0.9rem'}}>
                                <span>Less: DLA (director-funded costs):</span>
                                <span>-{formatCurrency(metrics.ctAllowableDlaNet)}</span>
                            </div>
                        )}
                        {((metrics.nonCtExpenseGross || 0) + (metrics.nonCtDlaGross || 0)) > 0 && (<>
                            <div
                                className="info-row"
                                style={{color: '#e65100', fontSize: '0.9rem', cursor: 'pointer'}}
                                onClick={() => setShowNonCtModal(true)}
                                title="These costs are disallowed for CT — HMRC does not allow them as deductions. VAT on these items is also blocked. The full gross amount is shown as it cannot be recovered. They are NOT removed from taxable profit, so your taxable profit is higher than accounting profit by this amount."
                            >
                                <span>⚠️ Disallowed items (not deductible for CT):</span>
                                <span style={{display:'flex', alignItems:'center', gap:6}}>
                                    {formatCurrency((metrics.nonCtExpenseGross || 0) + (metrics.nonCtDlaGross || 0))}
                                    <span style={{fontSize:11, fontWeight:400, opacity:0.8}}>🔍 view</span>
                                </span>
                            </div>
                            <div style={{fontSize:'0.77rem', color:'#888', paddingLeft:'0.75rem', marginBottom:'0.3rem', lineHeight:1.4}}>
                                ↳ Not deducted above — these costs remain in taxable profit (HMRC disallows them)
                            </div>
                        </>)}
                        <div className="info-row">
                            <span>Trading Profit <small style={{opacity:0.6}}>(CT basis)</small>:</span>
                            <span>{formatCurrency(metrics.tradingProfit)}</span>
                        </div>
                        <div className="info-row">
                            <span>Less: Salary &amp; Employer NI:</span>
                            <span>-{formatCurrency(metrics.salary + metrics.employerNI)}</span>
                        </div>
                        <div className="info-row">
                            <span><strong>Taxable Profit Before CT:</strong></span>
                            <span><strong>{formatCurrency(metrics.profitBeforeTax)}</strong></span>
                        </div>
                        {((metrics.nonCtExpenseGross || 0) + (metrics.nonCtDlaGross || 0)) > 0 && (
                            <div style={{fontSize:'0.77rem', color:'#888', paddingLeft:'0.75rem', marginBottom:'0.1rem', lineHeight:1.4}}>
                                ↳ Accounting PBT would be {formatCurrency(metrics.profitBeforeTax - (metrics.nonCtExpenseGross || 0) - (metrics.nonCtDlaGross || 0))} — taxable profit is {formatCurrency((metrics.nonCtExpenseGross || 0) + (metrics.nonCtDlaGross || 0))} higher due to disallowed items (gross incl. irrecoverable VAT)
                            </div>
                        )}
                        <div className="info-row" style={{ cursor: 'pointer' }} onClick={() => setShowCtModal(true)} title="Click for full CT breakdown">
                            <span>Corporation Tax (19-25%):</span>
                            <span style={{ display:'flex', alignItems:'center', gap:6 }}>
                                <span className="text-danger">{formatCurrency(metrics.corpTaxEstimate)}</span>
                                <span style={{fontSize:11, fontWeight:400, opacity:0.6}}>🔍 breakdown</span>
                            </span>
                        </div>
                        <div className="info-note">
                            <small>
                                {metrics.profitBeforeTax > 0 && metrics.profitBeforeTax <= 50000 && '19% rate applies (taxable profit ≤ £50k)'}
                                {metrics.profitBeforeTax > 50000 && metrics.profitBeforeTax < 250000 && 
                                    'Marginal relief applies (taxable profit between £50k-£250k)'}
                                {metrics.profitBeforeTax >= 250000 && '25% rate applies (taxable profit ≥ £250k)'}
                                {metrics.profitBeforeTax <= 0 && 'No CT due — taxable profit is zero or negative'}
                            </small>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        {/* ── NonCT Expenses Modal ── */}
        {showNonCtModal && metrics?.nonCtExpenseItems && (
            <div
                style={{
                    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.45)',
                    zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}
                onClick={() => setShowNonCtModal(false)}
            >
                <div
                    style={{
                        background: '#fff', borderRadius: 12, padding: '24px 28px',
                        maxWidth: 620, width: '95%', maxHeight: '80vh', overflow: 'auto',
                        boxShadow: '0 8px 32px rgba(0,0,0,0.18)'
                    }}
                    onClick={e => e.stopPropagation()}
                >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                        <h3 style={{ margin: 0, fontSize: '1.1rem' }}>⚠️ Disallowed (NonCT) Items</h3>
                        <button
                            onClick={() => setShowNonCtModal(false)}
                            style={{ background: 'none', border: 'none', fontSize: 22, cursor: 'pointer', color: '#6c757d' }}
                        >×</button>
                    </div>
                    <p style={{ fontSize: 13, color: '#6c757d', marginTop: 0 }}>
                        These expenses are tagged <strong>NonCT</strong> and are excluded from your Corporation Tax calculation.
                        Common examples: client entertainment, personal items, fines.
                    </p>
                    {metrics.nonCtExpenseItems.length === 0 ? (
                        <p>No NonCT expenses found.</p>
                    ) : (
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                            <thead>
                                <tr style={{ borderBottom: '2px solid #dee2e6' }}>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Date</th>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Description</th>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Category</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>Net</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>VAT</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>Gross</th>
                                </tr>
                            </thead>
                            <tbody>
                                {metrics.nonCtExpenseItems
                                    .sort((a, b) => new Date(b.entryDate) - new Date(a.entryDate))
                                    .map((exp, i) => (
                                        <tr key={exp.id ?? i} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                            <td style={{ padding: '6px 8px', whiteSpace: 'nowrap' }}>
                                                {exp.entryDate ? new Date(exp.entryDate).toLocaleDateString('en-GB') : '—'}
                                            </td>
                                            <td style={{ padding: '6px 8px' }}>{exp.description || exp.supplier || '—'}</td>
                                            <td style={{ padding: '6px 8px', color: '#6c757d' }}>{exp.category || '—'}</td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right' }}>
                                                {formatCurrency(exp.amountNet)}
                                            </td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right', color: '#6c757d' }}>
                                                {exp.vatAmount > 0 ? formatCurrency(exp.vatAmount) : '—'}
                                            </td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right', fontWeight: 600 }}>
                                                {formatCurrency(exp.amountGross)}
                                            </td>
                                        </tr>
                                    ))
                                }
                            </tbody>
                            <tfoot>
                                <tr style={{ borderTop: '2px solid #dee2e6', fontWeight: 700 }}>
                                    <td colSpan={3} style={{ padding: '8px 8px' }}>Total</td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#dc3545' }}>
                                        {formatCurrency(metrics.nonCtExpenseItems.reduce((s, e) => s + (e.amountNet || 0), 0))}
                                    </td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#6c757d' }}>
                                        {formatCurrency(metrics.nonCtExpenseItems.reduce((s, e) => s + (e.vatAmount || 0), 0))}
                                    </td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#dc3545' }}>
                                        {formatCurrency(metrics.nonCtExpenseItems.reduce((s, e) => s + (e.amountGross || 0), 0))}
                                    </td>
                                </tr>
                            </tfoot>
                        </table>
                    )}
                    {metrics.nonCtDlaItems && metrics.nonCtDlaItems.length > 0 && (<>
                        <h4 style={{ marginTop: 20, marginBottom: 8, fontSize: '0.95rem', color: '#495057' }}>
                            🏦 NonCT DLA Entries (Director Loan Account)
                        </h4>
                        <p style={{ fontSize: 12, color: '#6c757d', marginTop: 0 }}>
                            These DLA entries are tagged <strong>NonCT</strong> and are excluded from your CT deduction.
                            Their VAT (if any) is also blocked and not counted as reclaimable input tax.
                        </p>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                            <thead>
                                <tr style={{ borderBottom: '2px solid #dee2e6' }}>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Date</th>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Description</th>
                                    <th style={{ textAlign: 'left', padding: '6px 8px' }}>Category</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>Net</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>VAT</th>
                                    <th style={{ textAlign: 'right', padding: '6px 8px' }}>Gross</th>
                                </tr>
                            </thead>
                            <tbody>
                                {metrics.nonCtDlaItems
                                    .sort((a, b) => new Date(b.entryDate || b.datePaid || 0) - new Date(a.entryDate || a.datePaid || 0))
                                    .map((e, i) => (
                                        <tr key={e.id ?? i} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                            <td style={{ padding: '6px 8px', whiteSpace: 'nowrap' }}>
                                                {(e.entryDate || e.datePaid) ? new Date(e.entryDate || e.datePaid).toLocaleDateString('en-GB') : '—'}
                                            </td>
                                            <td style={{ padding: '6px 8px' }}>{e.description || e.supplier || '—'}</td>
                                            <td style={{ padding: '6px 8px', color: '#6c757d' }}>{e.category || '—'}</td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right' }}>{formatCurrency(e.amountNet)}</td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right', color: '#6c757d' }}>
                                                {e.vatAmount > 0 ? formatCurrency(e.vatAmount) : '—'}
                                            </td>
                                            <td style={{ padding: '6px 8px', textAlign: 'right', fontWeight: 600 }}>{formatCurrency(e.amountGross)}</td>
                                        </tr>
                                    ))
                                }
                            </tbody>
                            <tfoot>
                                <tr style={{ borderTop: '2px solid #dee2e6', fontWeight: 700 }}>
                                    <td colSpan={3} style={{ padding: '8px 8px' }}>Total</td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#dc3545' }}>
                                        {formatCurrency(metrics.nonCtDlaItems.reduce((s, e) => s + (e.amountNet || 0), 0))}
                                    </td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#6c757d' }}>
                                        {formatCurrency(metrics.nonCtDlaItems.reduce((s, e) => s + (e.vatAmount || 0), 0))}
                                    </td>
                                    <td style={{ padding: '8px 8px', textAlign: 'right', color: '#dc3545' }}>
                                        {formatCurrency(metrics.nonCtDlaItems.reduce((s, e) => s + (e.amountGross || 0), 0))}
                                    </td>
                                </tr>
                            </tfoot>
                        </table>
                    </>)}
                    <div style={{ marginTop: 16, fontSize: 12, color: '#888' }}>
                        To fix a NonCT tag, go to <strong>Expenses</strong> or <strong>DLA</strong> and edit the CT tag on the relevant record.
                    </div>
                </div>
            </div>
        )}
        {showTrivialBenefit && (
            <TrivialBenefitModal
                directors={directors}
                onClose={() => setShowTrivialBenefit(false)}
                onSaved={() => { setShowTrivialBenefit(false); loadData(); }}
            />
        )}

        {/* ── CT Breakdown Modal ── */}
        {showCtModal && metrics && (
            <div
                style={{ position:'fixed', inset:0, background:'rgba(0,0,0,0.45)', zIndex:1000, display:'flex', alignItems:'center', justifyContent:'center' }}
                onClick={() => setShowCtModal(false)}
            >
                <div
                    style={{ background:'#fff', borderRadius:12, padding:'24px 28px', maxWidth:520, width:'95%', boxShadow:'0 8px 32px rgba(0,0,0,0.18)' }}
                    onClick={e => e.stopPropagation()}
                >
                    <div style={{ display:'flex', justifyContent:'space-between', alignItems:'center', marginBottom:20 }}>
                        <h3 style={{ margin:0, fontSize:'1.1rem' }}>🏛️ Corporation Tax Breakdown</h3>
                        <button onClick={() => setShowCtModal(false)} style={{ background:'none', border:'none', fontSize:22, cursor:'pointer', color:'#6c757d' }}>×</button>
                    </div>

                    {/* Calculation table */}
                    <table style={{ width:'100%', borderCollapse:'collapse', fontSize:'0.9rem' }}>
                        <tbody>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', color:'#495057' }}>Income (net, excl. VAT)</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', fontWeight:500 }}>{formatCurrency(metrics.ctIncomeNet)}</td>
                            </tr>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', color:'#495057' }}>Less: CT-allowable expenses</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', color:'#dc3545' }}>−{formatCurrency(metrics.ctAllowableExpenseNet)}</td>
                            </tr>
                            {metrics.ctAllowableDlaNet > 0 && (
                                <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                    <td style={{ padding:'7px 4px', color:'#16a34a' }}>Less: DLA (director-funded costs)</td>
                                    <td style={{ padding:'7px 4px', textAlign:'right', color:'#16a34a' }}>−{formatCurrency(metrics.ctAllowableDlaNet)}</td>
                                </tr>
                            )}
                            <tr style={{ borderBottom:'2px solid #dee2e6' }}>
                                <td style={{ padding:'7px 4px', fontWeight:600 }}>Trading Profit (CT basis)</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', fontWeight:600 }}>{formatCurrency(metrics.tradingProfit)}</td>
                            </tr>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', color:'#495057' }}>Less: Salary (gross)</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', color:'#dc3545' }}>−{formatCurrency(metrics.salary)}</td>
                            </tr>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', color:'#495057' }}>Less: Employer NI</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', color:'#dc3545' }}>−{formatCurrency(metrics.employerNI)}</td>
                            </tr>
                            <tr style={{ borderBottom:'2px solid #dee2e6', background:'#f8f9fa' }}>
                                <td style={{ padding:'8px 4px', fontWeight:700 }}>Taxable Profit Before CT</td>
                                <td style={{ padding:'8px 4px', textAlign:'right', fontWeight:700 }}>{formatCurrency(metrics.profitBeforeTax)}</td>
                            </tr>
                            {((metrics.nonCtExpenseNet || 0) + (metrics.nonCtDlaNet || 0)) > 0 && (
                                <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                    <td style={{ padding:'7px 4px', color:'#e65100', fontSize:'0.85rem' }}>⚠️ Disallowed items (in taxable profit)</td>
                                    <td style={{ padding:'7px 4px', textAlign:'right', color:'#e65100', fontSize:'0.85rem' }}>+{formatCurrency((metrics.nonCtExpenseNet||0)+(metrics.nonCtDlaNet||0))}</td>
                                </tr>
                            )}
                        </tbody>
                    </table>

                    {/* Rate box */}
                    <div style={{ margin:'16px 0', padding:'10px 14px', background:'#f8f9fa', borderRadius:8, fontSize:'0.875rem', color:'#495057' }}>
                        {metrics.profitBeforeTax <= 0 && <span>No CT due — taxable profit is zero or negative</span>}
                        {metrics.profitBeforeTax > 0 && metrics.profitBeforeTax <= 50000 && <span>Rate: <strong>19%</strong> — small profits rate (taxable profit ≤ £50,000)</span>}
                        {metrics.profitBeforeTax > 50000 && metrics.profitBeforeTax < 250000 && <span>Rate: <strong>Marginal relief</strong> — between £50k–£250k (19%→25% taper)</span>}
                        {metrics.profitBeforeTax >= 250000 && <span>Rate: <strong>25%</strong> — main rate (taxable profit ≥ £250,000)</span>}
                    </div>

                    {/* CT summary */}
                    <table style={{ width:'100%', borderCollapse:'collapse', fontSize:'0.9rem' }}>
                        <tbody>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', fontWeight:600 }}>Corporation Tax Estimate</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', fontWeight:600, color:'#dc3545' }}>{formatCurrency(metrics.corpTaxEstimate)}</td>
                            </tr>
                            <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                <td style={{ padding:'7px 4px', color:'#495057' }}>CT Paid to date</td>
                                <td style={{ padding:'7px 4px', textAlign:'right', color:'#16a34a' }}>−{formatCurrency(metrics.corpTaxPaid)}</td>
                            </tr>
                            <tr style={{ borderBottom:'2px solid #dee2e6', background:'#fff3cd' }}>
                                <td style={{ padding:'8px 4px', fontWeight:700 }}>CT Outstanding</td>
                                <td style={{ padding:'8px 4px', textAlign:'right', fontWeight:700, color: metrics.corpTaxDue > 0 ? '#dc3545' : '#16a34a' }}>{formatCurrency(metrics.corpTaxDue)}</td>
                            </tr>
                        </tbody>
                    </table>

                    {/* Post-tax distribution */}
                    {(() => {
                        const postTaxProfit = metrics.profitBeforeTax - metrics.corpTaxEstimate;
                        const retained = postTaxProfit - (metrics.dividendsDeclared || 0);
                        return (
                            <div style={{ marginTop:18 }}>
                                <div style={{ fontSize:'0.78rem', fontWeight:600, color:'#6c757d', textTransform:'uppercase', letterSpacing:'0.04em', marginBottom:8 }}>
                                    Post-Tax Distribution
                                </div>
                                <table style={{ width:'100%', borderCollapse:'collapse', fontSize:'0.9rem' }}>
                                    <tbody>
                                        <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                            <td style={{ padding:'6px 4px', color:'#495057' }}>Post-tax profit</td>
                                            <td style={{ padding:'6px 4px', textAlign:'right' }}>{formatCurrency(postTaxProfit)}</td>
                                        </tr>
                                        <tr style={{ borderBottom:'1px solid #f0f0f0' }}>
                                            <td style={{ padding:'6px 4px', color:'#495057' }}>
                                                Dividends declared
                                                <span style={{ fontSize:'0.73rem', color:'#888', marginLeft:6 }}>(not CT-deductible)</span>
                                            </td>
                                            <td style={{ padding:'6px 4px', textAlign:'right', color:'#6c757d' }}>−{formatCurrency(metrics.dividendsDeclared || 0)}</td>
                                        </tr>
                                        <tr style={{ background:'#f8f9fa' }}>
                                            <td style={{ padding:'7px 4px', fontWeight:600 }}>Retained earnings (est.)</td>
                                            <td style={{ padding:'7px 4px', textAlign:'right', fontWeight:600, color: retained >= 0 ? '#16a34a' : '#dc3545' }}>{formatCurrency(retained)}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        );
                    })()}

                    {/* CT deductibility guide */}
                    <div style={{ marginTop:16, padding:'10px 12px', background:'#f8f9fa', borderRadius:8, fontSize:'0.8rem', color:'#495057', lineHeight:1.7 }}>
                        <strong>CT deductibility:</strong><br/>
                        ✅ Salary (gross) — CT deductible<br/>
                        ✅ Employer's NI — CT deductible<br/>
                        ✅ Employer pension contributions — CT deductible<br/>
                        ❌ Employee's NI — deducted from gross salary, not an extra company cost<br/>
                        ❌ Dividends — paid from post-tax profit, never CT-deductible
                    </div>

                    <p style={{ fontSize:'0.75rem', color:'#888', marginTop:12, marginBottom:0 }}>
                        This is an estimate based on year-to-date figures. Consult your accountant for the final CT600 return.
                    </p>
                </div>
            </div>
        )}
        </>
    );
}
