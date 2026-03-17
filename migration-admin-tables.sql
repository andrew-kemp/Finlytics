-- Create AdminUsers table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdminUsers')
BEGIN
    CREATE TABLE [dbo].[AdminUsers](
        [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Username] [nvarchar](100) NOT NULL,
        [PasswordHash] [nvarchar](500) NULL,
        [Email] [nvarchar](255) NULL,
        [FullName] [nvarchar](255) NULL,
        [IsActive] [bit] NOT NULL DEFAULT 1,
        [IsSuperAdmin] [bit] NOT NULL DEFAULT 0,
        [MfaSecret] [nvarchar](200) NULL,
        [PasskeyCredentialId] [nvarchar](500) NULL,
        [PasskeyPublicKey] [nvarchar](2000) NULL,
        [CreatedDate] [datetime2](7) NULL,
        [LastLoginDate] [datetime2](7) NULL,
        [LastPasswordChange] [datetime2](7) NULL,
        [MustChangePassword] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [UQ_AdminUsers_Username] UNIQUE ([Username]),
        CONSTRAINT [UQ_AdminUsers_Email] UNIQUE ([Email])
    );
    
    PRINT 'AdminUsers table created successfully';
END
ELSE
BEGIN
    PRINT 'AdminUsers table already exists';
END
GO

-- Create AdminSessions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdminSessions')
BEGIN
    CREATE TABLE [dbo].[AdminSessions](
        [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Token] [nvarchar](100) NOT NULL,
        [AdminUserId] [int] NOT NULL,
        [CreatedDate] [datetime2](7) NULL,
        [ExpiresAt] [datetime2](7) NULL,
        CONSTRAINT [FK_AdminSessions_AdminUsers] FOREIGN KEY ([AdminUserId]) 
            REFERENCES [dbo].[AdminUsers] ([Id]) ON DELETE CASCADE
    );
    
    PRINT 'AdminSessions table created successfully';
END
ELSE
BEGIN
    PRINT 'AdminSessions table already exists';
END
GO

-- Insert default admin user if not exists
IF NOT EXISTS (SELECT * FROM AdminUsers WHERE Username = 'admin')
BEGIN
    INSERT INTO [dbo].[AdminUsers] 
        ([Username], [PasswordHash], [Email], [FullName], [IsActive], [IsSuperAdmin], [CreatedDate], [MustChangePassword])
    VALUES 
        ('admin', NULL, 'admin@financehub.local', 'System Administrator', 1, 1, GETUTCDATE(), 1);
    
    PRINT 'Default admin user created successfully';
END
ELSE
BEGIN
    PRINT 'Admin user already exists';
END
GO

-- Add SSO columns to FinanceHubSettings if they don't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'AuthenticationType')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [AuthenticationType] [nvarchar](50) NULL;
    
    PRINT 'AuthenticationType column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'SsoEnabled')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [SsoEnabled] [bit] NOT NULL DEFAULT 0;
    
    PRINT 'SsoEnabled column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'AzureAdTenantId')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [AzureAdTenantId] [nvarchar](100) NULL;
    
    PRINT 'AzureAdTenantId column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'AzureAdClientId')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [AzureAdClientId] [nvarchar](100) NULL;
    
    PRINT 'AzureAdClientId column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'AzureAdRedirectUri')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [AzureAdRedirectUri] [nvarchar](500) NULL;
    
    PRINT 'AzureAdRedirectUri column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'RequireMfa')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [RequireMfa] [bit] NOT NULL DEFAULT 1;
    
    PRINT 'RequireMfa column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'RequirePasskey')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [RequirePasskey] [bit] NOT NULL DEFAULT 0;
    
    PRINT 'RequirePasskey column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'MfaProvider')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [MfaProvider] [nvarchar](50) NULL DEFAULT 'TOTP';
    
    PRINT 'MfaProvider column added';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FinanceHubSettings') AND name = 'PasskeyProvider')
BEGIN
    ALTER TABLE [dbo].[FinanceHubSettings] 
    ADD [PasskeyProvider] [nvarchar](50) NULL DEFAULT 'WebAuthn';
    
    PRINT 'PasskeyProvider column added';
END
GO

PRINT 'Migration completed successfully!';
