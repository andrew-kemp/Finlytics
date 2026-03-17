namespace FinanceHubFunctions.Models
{
    public class Supplier
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SupplierCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Category { get; set; }
        public int? DefaultVATRate { get; set; }
        
        // New Payees fields
        public string? PayeeType { get; set; }
        public bool IsActive { get; set; } = true;
        public bool OnHold { get; set; } = false;
        public string? PrimaryContact { get; set; }
        public string? RemittanceEmail { get; set; }
        public string? Phone { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Currency { get; set; }
        public string? VATRegistration { get; set; }
        public string? AccountNumber { get; set; }
        public string? SortCode { get; set; }
        public string? IBAN { get; set; }
    }
}
