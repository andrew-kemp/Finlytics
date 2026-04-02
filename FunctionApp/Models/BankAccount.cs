using System;

namespace FinanceHubFunctions.Models
{
    public class BankAccount
    {
        public int Id { get; set; }
        public string? AccountName { get; set; }
        public string? BankName { get; set; }
        public string? SortCode { get; set; }
        public string? AccountNumber { get; set; }
        public string? Currency { get; set; }
        public decimal? OpeningBalance { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        // Monzo integration
        public string? MonzoAccountId { get; set; }
        public bool MonzoConnected { get; set; } = false;
        public DateTime? MonzoLastSyncedAt { get; set; }
        // TrueLayer integration
        public string? TrueLayerAccountId { get; set; }
        public bool TrueLayerConnected { get; set; } = false;
        public DateTime? TrueLayerLastSyncedAt { get; set; }
        public string? TrueLayerProvider { get; set; } // e.g. "Monzo", "Starling", "HSBC"
        // GoCardless Bank Account Data (ex-Nordigen)
        public string? GoCardlessRequisitionId { get; set; }
        public string? GoCardlessAccountId { get; set; }
        public bool GoCardlessConnected { get; set; } = false;
        public DateTime? GoCardlessLastSyncedAt { get; set; }
        public string? GoCardlessInstitutionId { get; set; } // e.g. "MONZO_MONZGB2L"
    }
}
