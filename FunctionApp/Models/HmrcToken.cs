using System;

namespace FinanceHubFunctions.Models
{
    /// <summary>
    /// Stores the HMRC OAuth 2.0 access and refresh tokens.
    /// Only one record exists at a time — updated on each auth/refresh.
    /// </summary>
    public class HmrcToken
    {
        public int Id { get; set; }
        public string AccessToken { get; set; } = "";
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Scope { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Payload sent to HMRC MTD VAT submission endpoint.
    /// All monetary amounts in pounds (2dp). Integer values rounded to nearest pound.
    /// </summary>
    public class HmrcVatSubmission
    {
        public string PeriodKey { get; set; } = "";          // HMRC obligation period key e.g. "24AA"
        public decimal VatDueSales { get; set; }             // Box 1: VAT charged on sales
        public decimal VatDueAcquisitions { get; set; }      // Box 2: VAT on EU acquisitions (0 for domestic)
        public decimal TotalVatDue { get; set; }             // Box 3: = Box1 + Box2
        public decimal VatReclaimedCurrPeriod { get; set; }  // Box 4: Input VAT reclaimed
        public decimal NetVatDue { get; set; }               // Box 5: |Box3 - Box4|
        public long TotalValueSalesExVAT { get; set; }       // Box 6: Net sales (whole pounds)
        public long TotalValuePurchasesExVAT { get; set; }   // Box 7: Net purchases (whole pounds)
        public long TotalValueGoodsSuppliedExVAT { get; set; } // Box 8: Goods supplied to EU (0)
        public long TotalAcquisitionsExVAT { get; set; }     // Box 9: EU acquisitions (0)
        public bool Finalised { get; set; } = true;
    }
}
