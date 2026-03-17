using System;

namespace FinanceHubFunctions.Models
{
    public class Asset
    {
        public int Id { get; set; }
        public string? AssetId { get; set; }  // Auto-generated unique ID like AST-001
        public string? Name { get; set; }
        public string? Category { get; set; }  // IT Equipment, Furniture, Vehicle, etc.
        public string? Description { get; set; }
        public string? SerialNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? SupplierId { get; set; }  // Link to Supplier
        public string? SupplierName { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public string? AssignedToEmployeeId { get; set; }  // Link to Employee
        public string? AssignedToEmployeeName { get; set; }
        public string? Location { get; set; }
        public string? DepreciationMethod { get; set; }  // Straight-line, Reducing balance, None
        public int? UsefulLifeYears { get; set; }
        public decimal? ResidualValue { get; set; }
        public decimal? CurrentValue { get; set; }
        public string? Status { get; set; }  // In Use, In Storage, Disposed, Lost, Under Repair
        public DateTime? DisposalDate { get; set; }
        public decimal? DisposalValue { get; set; }
        public string? DisposalMethod { get; set; }  // Sold, Scrapped, Donated, Lost
        public string? Notes { get; set; }
        public string? InvoiceUrl { get; set; }  // Blob URL of the purchase invoice
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
