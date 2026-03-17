using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceHubFunctions.Models
{
    public enum DeclarationType
    {
        MissingReceiptDeclaration,
        DirectorExpenseDeclaration
    }

    public enum ReceiptMissingReason
    {
        NotProvided,
        Lost,
        DigitalUnavailable,
        Other
    }

    public enum DeclarationSignatureType
    {
        TypedName,
        None
    }

    public enum DeclarationStatus
    {
        Draft,
        Finalised,
        Voided
    }

    public class MissingReceiptDeclaration
    {
        public int Id { get; set; }

        /// <summary>Human-readable reference, e.g. MRD-20260315-001</summary>
        [MaxLength(50)]
        public string? DeclarationId { get; set; }

        /// <summary>FK to Expenses.Id — null for DLA entry declarations</summary>
        public int? ExpenseId { get; set; }

        /// <summary>FK to DlaEntries.Id — null for expense declarations</summary>
        public int? DlaEntryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DeclarationType DeclarationType { get; set; } = DeclarationType.MissingReceiptDeclaration;

        // Declarer details
        [MaxLength(200)]
        public string? DeclarerName { get; set; }

        [MaxLength(100)]
        public string? DeclarerRole { get; set; }

        [MaxLength(256)]
        public string? DeclarerEmail { get; set; }

        // Expense details captured at time of declaration
        public decimal AmountGross { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "GBP";

        public DateTime? ExpenseDate { get; set; }

        [MaxLength(200)]
        public string? MerchantOrPayee { get; set; }

        [MaxLength(100)]
        public string? BankTransactionRef { get; set; }

        [MaxLength(100)]
        public string? ExpenseCategory { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        // Receipt reason
        public ReceiptMissingReason ReasonReceiptMissing { get; set; } = ReceiptMissingReason.NotProvided;

        [MaxLength(500)]
        public string? OtherReasonText { get; set; }

        // VAT enforcement — always false/zero, enforced in service layer
        public bool VatReclaimable { get; set; } = false;
        public decimal VatAmount { get; set; } = 0m;

        // Acknowledgement
        public bool AcknowledgementDisallowable { get; set; } = false;

        // Signature
        public DeclarationSignatureType SignatureType { get; set; } = DeclarationSignatureType.TypedName;

        [MaxLength(200)]
        public string? TypedSignature { get; set; }

        // Audit/storage
        [MaxLength(500)]
        public string? PdfBlobRef { get; set; }

        [MaxLength(64)]
        public string? HashSha256 { get; set; }

        public DeclarationStatus Status { get; set; } = DeclarationStatus.Draft;

        public DateTime? FinalisedAt { get; set; }
        public DateTime? VoidedAt { get; set; }

        [MaxLength(500)]
        public string? VoidedReason { get; set; }
    }
}
