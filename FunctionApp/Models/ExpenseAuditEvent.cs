using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceHubFunctions.Models
{
    public enum ExpenseAuditEventType
    {
        DeclarationCreated,
        DeclarationFinalised,
        VatDisabledDueToMissingReceipt,
        DeclarationVoided,
        ReceiptAttached,
        ExpenseCreated,
        ExpenseUpdated
    }

    public class ExpenseAuditEvent
    {
        public int Id { get; set; }

        /// <summary>FK to Expenses.Id</summary>
        public int ExpenseId { get; set; }

        public ExpenseAuditEventType EventType { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? ActorName { get; set; }

        [MaxLength(256)]
        public string? ActorEmail { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }

        /// <summary>Optional FK to MissingReceiptDeclarations.Id if the event is declaration-related</summary>
        public int? DeclarationId { get; set; }
    }
}
