using System;

namespace FinanceHubFunctions.Models
{
    public class Shareholder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShareholderType { get; set; }
        public int? ShareClassId { get; set; }
        public string ShareClassName { get; set; }
        public bool IsActive { get; set; }
        public int SharesOwned { get; set; }
        public string ShareCertificateNumber { get; set; }
        public DateTime? DateOfIssue { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }

        // Bank details for BACS dividend payments
        public string? BankAccountName { get; set; }
        public string? BankSortCode { get; set; }
        public string? AccountNumber { get; set; }
    }

    public class ShareClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string VotingRights { get; set; }
        public string DividendPolicyNote { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
    }
}
