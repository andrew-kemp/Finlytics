using System;

namespace FinanceHubFunctions.Models
{
    /// <summary>
    /// System-wide settings for FinanceHub (SMTP, SAML, API keys, etc.)
    /// This is separate from CompanySettings which is company-specific data
    /// </summary>
    public class FinanceHubSettings
    {
        public int Id { get; set; }
        
        // SMTP Configuration (3rd party like SMTP2Go, SendGrid, etc.)
        public string? SmtpProvider { get; set; } // e.g., "SMTP2Go", "SendGrid", "Custom"
        public string? SmtpServer { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; } // Encrypted in storage
        public string? SmtpFromAddress { get; set; }
        public string? SmtpFromName { get; set; }
        public bool? SmtpUseTLS { get; set; }
        public bool? SmtpUseSSL { get; set; }
        
        // API Keys for external services
        public string? PaymentGatewayApiKey { get; set; }
        public string? PaymentGatewayProvider { get; set; } // Stripe, PayPal, etc.
        
        // GoCardless Configuration
        public string? GoCardlessAccessToken { get; set; }
        public string? GoCardlessSecretId { get; set; }
        public string? GoCardlessSecretKey { get; set; }
        public string? GoCardlessWebhookSecret { get; set; }
        public bool GoCardlessSandbox { get; set; } = true;
        public bool GoCardlessBankDataEnabled { get; set; } = false;
        public bool GoCardlessPaymentsEnabled { get; set; } = false;
        
        // Storage Configuration
        public string? BlobStorageConnectionString { get; set; }
        public string? InvoicesContainerName { get; set; }
        public string? ReceiptsContainerName { get; set; }
        public string? CertificatesContainerName { get; set; }
        
        // System Settings
        public string? DefaultTimeZone { get; set; }
        public string? DateFormat { get; set; }
        public string? TimeFormat { get; set; }
        public int? SessionTimeoutMinutes { get; set; }
        public bool? MaintenanceMode { get; set; }
        public string? MaintenanceMessage { get; set; }
        
        // Audit
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
