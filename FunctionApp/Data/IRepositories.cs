using FinanceHubFunctions.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinanceHubFunctions.Data
{
    public interface ICustomerRepository
    {
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer?> GetByIdAsync(string id);
        Task<Customer?> GetByCodeAsync(string code);
        Task<Customer> CreateAsync(Customer customer);
        Task<Customer> UpdateAsync(Customer customer);
        Task DeleteAsync(string id);
    }

    public interface ISupplierRepository
    {
        Task<IEnumerable<Supplier>> GetAllAsync();
        Task<Supplier?> GetByIdAsync(string id);
        Task<Supplier?> GetByCodeAsync(string code);
        Task<Supplier> CreateAsync(Supplier supplier);
        Task<Supplier> UpdateAsync(Supplier supplier);
        Task DeleteAsync(string id);
    }

    public interface IInvoiceRepository
    {
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task<Invoice?> GetByIdAsync(int id);
        Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<IEnumerable<Invoice>> GetByFinancialYearAsync(string financialYear);
        Task<IEnumerable<Invoice>> GetByStatusAsync(string status);
        Task<IEnumerable<Invoice>> GetOverdueAsync();
        Task<Invoice> CreateAsync(Invoice invoice);
        Task<Invoice> UpdateAsync(Invoice invoice);
        Task DeleteAsync(int id);
    }

    public interface IExpenseRepository
    {
        Task<IEnumerable<Expense>> GetAllAsync();
        Task<Expense?> GetByIdAsync(int id);
        Task<IEnumerable<Expense>> GetByFinancialYearAsync(string financialYear);
        Task<IEnumerable<Expense>> GetByCategoryAsync(string category);
        Task<IEnumerable<Expense>> GetDueRecurringAsync();
        Task<Expense> CreateAsync(Expense expense);
        Task<Expense> UpdateAsync(Expense expense);
        Task DeleteAsync(int id);
        /// <summary>Direct SQL UPDATE — bypasses EF Core change tracker to reliably mark a declaration on file and zero VAT.</summary>
        Task<bool> MarkDeclarationFiledAsync(int id, string declarationRef);
    }

    public interface IDlaRepository
    {
        Task<IEnumerable<DlaEntry>> GetAllAsync();
        Task<DlaEntry?> GetByIdAsync(int id);
        Task<DlaEntry?> GetByDlaIdAsync(string dlaId);
        Task<string> GenerateNextDlaIdAsync();
        Task<IEnumerable<DlaEntry>> GetByPeriodKeyAsync(string periodKey);
        Task<IEnumerable<DlaEntry>> GetByFinancialYearAsync(string financialYear);
        Task<DlaEntry> CreateAsync(DlaEntry dlaEntry);
        Task<DlaEntry> UpdateAsync(DlaEntry dlaEntry);
        Task DeleteAsync(int id);
        /// <summary>Direct SQL UPDATE — bypasses EF Core change tracker to reliably mark a declaration on file and zero VAT.</summary>
        Task<bool> MarkDeclarationFiledAsync(int id, string declarationRef);
    }

    public interface IQuoteRepository
    {
        Task<IEnumerable<Quote>> GetAllAsync();
        Task<Quote?> GetByIdAsync(int id);
        Task<Quote?> GetByQuoteNumberAsync(string quoteNumber);
        Task<IEnumerable<Quote>> GetByStatusAsync(string status);
        Task<Quote> CreateAsync(Quote quote);
        Task<Quote> UpdateAsync(Quote quote);
        Task DeleteAsync(int id);
    }

    public interface IDlaPaymentRepository
    {
        Task<List<DlaPayment>> GetAllAsync();
        Task<List<DlaPayment>> GetByDlaIdAsync(string dlaId);
        Task<DlaPayment?> GetByIdAsync(int id);
        Task<DlaPayment> CreateAsync(DlaPayment payment);
        Task<DlaPayment> UpdateAsync(DlaPayment payment);
        Task DeleteAsync(int id);
        Task<string> GenerateNextPaymentIdAsync();
        Task<List<DlaPayment>> GetByPeriodAsync(string periodKey);
        Task<decimal> GetTotalPaymentsForDlaAsync(string dlaId);
    }

    public interface ICompanyLedgerRepository
    {
        Task<IEnumerable<CompanyLedgerEntry>> GetByPeriodAsync(string periodKey);
        Task<IEnumerable<CompanyLedgerEntry>> GetByTaxYearAsync(int taxYear);
        Task<CompanyLedgerEntry?> GetByIdAsync(int id);
        Task<CompanyLedgerEntry> CreateAsync(CompanyLedgerEntry entry);
        Task<CompanyLedgerEntry> UpdateAsync(CompanyLedgerEntry entry);
        Task DeleteAsync(int id);
        Task<CompanyAggregates> GetAggregatesAsync(string periodKey);
        Task<CompanyAggregates> GetYtdAggregatesAsync(int taxYear);
    }

    public interface IShareholderRepository
    {
        Task<IEnumerable<Shareholder>> GetAllAsync();
        Task<Shareholder?> GetByIdAsync(int id);
        Task<IEnumerable<Shareholder>> GetActiveAsync();
        Task<Shareholder> CreateAsync(Shareholder shareholder);
        Task<Shareholder> UpdateAsync(Shareholder shareholder);
        Task DeleteAsync(int id);
    }

    public interface ICompanySettingsRepository
    {
        Task<CompanySettings?> GetDefaultAsync();
        Task<CompanySettings?> GetByIdAsync(int id);
        Task<CompanySettings> UpdateAsync(CompanySettings settings);
    }

    public interface IEmployeeRepository
    {
        Task<IEnumerable<Employee>> GetAllAsync();
        Task<Employee?> GetByIdAsync(int id);
        Task<IEnumerable<Employee>> GetDirectorsAsync();
        Task<Employee> CreateAsync(Employee employee);
        Task<Employee> UpdateAsync(Employee employee);
        Task DeleteAsync(int id);
        Task<string> GenerateNextEmployeeNumberAsync();
    }

    public interface IAssetRepository
    {
        Task<IEnumerable<Asset>> GetAllAsync();
        Task<Asset?> GetByIdAsync(int id);
        Task<Asset?> GetByAssetIdAsync(string assetId);
        Task<IEnumerable<Asset>> GetByCategoryAsync(string category);
        Task<IEnumerable<Asset>> GetByStatusAsync(string status);
        Task<Asset> CreateAsync(Asset asset);
        Task<Asset> UpdateAsync(Asset asset);
        Task DeleteAsync(int id);
        Task<string> GenerateNextAssetIdAsync();
    }

    public interface ISubscriptionRepository
    {
        Task<IEnumerable<Subscription>> GetAllAsync();
        Task<Subscription?> GetByIdAsync(int id);
        Task<Subscription?> GetBySubscriptionIdAsync(string subscriptionId);
        Task<IEnumerable<Subscription>> GetByTypeAsync(string type);
        Task<IEnumerable<Subscription>> GetByStatusAsync(string status);
        Task<IEnumerable<Subscription>> GetExpiringWithinDaysAsync(int days);
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription> UpdateAsync(Subscription subscription);
        Task DeleteAsync(int id);
        Task<string> GenerateNextSubscriptionIdAsync();
    }

    public interface IBankAccountRepository
    {
        Task<IEnumerable<BankAccount>> GetAllAsync();
        Task<BankAccount?> GetByIdAsync(int id);
        Task<BankAccount> CreateAsync(BankAccount account);
        Task<BankAccount> UpdateAsync(BankAccount account);
        Task DeleteAsync(int id);
    }

    public interface IBankTransactionRepository
    {
        Task<IEnumerable<BankTransaction>> GetAllAsync();
        Task<IEnumerable<BankTransaction>> GetByAccountIdAsync(int bankAccountId);
        Task<IEnumerable<BankTransaction>> GetUnreconciledAsync();
        Task<BankTransaction?> GetByIdAsync(int id);
        Task<BankTransaction> CreateAsync(BankTransaction transaction);
        Task<IEnumerable<BankTransaction>> CreateManyAsync(IEnumerable<BankTransaction> transactions);
        Task<BankTransaction> UpdateAsync(BankTransaction transaction);
        Task DeleteAsync(int id);
    }

    public interface IReconciliationRuleRepository
    {
        Task<IEnumerable<ReconciliationRule>> GetAllAsync();
        Task<ReconciliationRule?> GetByIdAsync(int id);
        Task<ReconciliationRule> CreateAsync(ReconciliationRule rule);
        Task<ReconciliationRule> UpdateAsync(ReconciliationRule rule);
        Task DeleteAsync(int id);
    }

    public interface IReconciliationMatchRepository
    {
        Task<IEnumerable<ReconciliationMatch>> GetByTransactionIdAsync(int transactionId);
        Task<ReconciliationMatch> CreateAsync(ReconciliationMatch match);
        Task DeleteAsync(int id);
    }

    public interface IPayrollRunRepository
    {
        Task<IEnumerable<PayrollRun>> GetAllAsync();
        Task<PayrollRun?> GetByIdAsync(int id);
        Task<PayrollRun> CreateAsync(PayrollRun run);
        Task<PayrollRun> UpdateAsync(PayrollRun run);
        Task DeleteAsync(int id);
    }

    public interface IPayslipRepository
    {
        Task<IEnumerable<Payslip>> GetByPayrollRunIdAsync(int payrollRunId);
        Task<Payslip?> GetByIdAsync(int id);
        Task<Payslip> CreateAsync(Payslip payslip);
        Task<Payslip> UpdateAsync(Payslip payslip);
        Task DeleteAsync(int id);
    }

    public interface IPayrollSettingsRepository
    {
        Task<PayrollSettings?> GetAsync();
        Task<PayrollSettings> UpdateAsync(PayrollSettings settings);
    }

    public interface IVatReturnRepository
    {
        Task<IEnumerable<VatReturn>> GetAllAsync();
        Task<VatReturn?> GetByIdAsync(int id);
        Task<VatReturn> CreateAsync(VatReturn vatReturn);
        Task<VatReturn> UpdateAsync(VatReturn vatReturn);
        Task DeleteAsync(int id);
    }

    public interface IMileageTripRepository
    {
        Task<IEnumerable<MileageTrip>> GetAllAsync();
        Task<IEnumerable<MileageTrip>> GetByTaxYearAsync(string taxYear);
        Task<IEnumerable<MileageTrip>> GetByDirectorAndTaxYearAsync(string director, string taxYear);
        Task<IEnumerable<MileageTrip>> GetDraftTripsByDirectorAsync(string director);
        Task<MileageTrip?> GetByIdAsync(int id);
        Task<MileageTrip> CreateAsync(MileageTrip trip);
        Task<MileageTrip> UpdateAsync(MileageTrip trip);
        Task DeleteAsync(int id);
        Task<decimal> GetCumulativeMilesByTaxYearAsync(string director, string taxYear);
        Task<string> GenerateNextTripIdAsync();
    }

    public interface IMileageClaimRepository
    {
        Task<IEnumerable<MileageClaim>> GetAllAsync();
        Task<IEnumerable<MileageClaim>> GetByDirectorAsync(string director);
        Task<MileageClaim?> GetByIdAsync(int id);
        Task<MileageClaim> CreateAsync(MileageClaim claim);
        Task<MileageClaim> UpdateAsync(MileageClaim claim);
        Task DeleteAsync(int id);
        Task<string> GenerateNextClaimRefAsync();
    }

    public interface IMissingReceiptDeclarationRepository
    {
        Task<MissingReceiptDeclaration?> GetByExpenseIdAsync(int expenseId);
        Task<MissingReceiptDeclaration?> GetByDlaEntryIdAsync(int dlaEntryId);
        Task<MissingReceiptDeclaration?> GetByIdAsync(int id);
        Task<MissingReceiptDeclaration> CreateAsync(MissingReceiptDeclaration declaration);
        Task<MissingReceiptDeclaration> UpdateAsync(MissingReceiptDeclaration declaration);
        Task<string> GenerateNextDeclarationIdAsync();
    }

    public interface IExpenseAuditEventRepository
    {
        Task<IEnumerable<ExpenseAuditEvent>> GetByExpenseIdAsync(int expenseId);
        Task<ExpenseAuditEvent> CreateAsync(ExpenseAuditEvent auditEvent);
    }

    public interface ICreditNoteRepository
    {
        Task<IEnumerable<CreditNote>> GetAllAsync();
        Task<CreditNote?> GetByIdAsync(int id);
        Task<CreditNote?> GetByCreditNoteNumberAsync(string number);
        Task<IEnumerable<CreditNote>> GetByCustomerIdAsync(string customerId);
        Task<IEnumerable<CreditNote>> GetByStatusAsync(string status);
        Task<IEnumerable<CreditNote>> GetPendingByCustomerIdAsync(string customerId);
        Task<CreditNote> CreateAsync(CreditNote creditNote);
        Task<CreditNote> UpdateAsync(CreditNote creditNote);
        Task DeleteAsync(int id);
        Task<string> GenerateNextCreditNoteNumberAsync();
    }
}
