-- ============================================================
-- Add Accountant & CompanyAccountant tables
-- Enables external accountants to view company data (read-only)
-- Run against: financehub-sql-kemponline / financehub
-- ============================================================

-- 1. Accountants table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Accountants')
BEGIN
    CREATE TABLE [dbo].[Accountants] (
        [Id]           INT            IDENTITY(1,1) NOT NULL,
        [ClerkUserId]  NVARCHAR(200)  NULL,
        [Email]        NVARCHAR(256)  NOT NULL,
        [Name]         NVARCHAR(200)  NOT NULL DEFAULT '',
        [FirmName]     NVARCHAR(300)  NULL,
        [Status]       NVARCHAR(50)   NOT NULL DEFAULT 'Invited',
        [CreatedAt]    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [AcceptedAt]   DATETIME2      NULL,

        CONSTRAINT [PK_Accountants] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_Accountants_ClerkUserId] ON [dbo].[Accountants] ([ClerkUserId]);
    CREATE UNIQUE INDEX [IX_Accountants_Email] ON [dbo].[Accountants] ([Email]);

    PRINT 'Created table: Accountants';
END
ELSE
    PRINT 'Table Accountants already exists — skipped.';
GO

-- 2. CompanyAccountants junction table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CompanyAccountants')
BEGIN
    CREATE TABLE [dbo].[CompanyAccountants] (
        [Id]            INT            IDENTITY(1,1) NOT NULL,
        [CompanyId]     INT            NOT NULL,      -- FK to CompanySettings.Id
        [AccountantId]  INT            NOT NULL,      -- FK to Accountants.Id
        [AccessLevel]   NVARCHAR(50)   NOT NULL DEFAULT 'ReadOnly',
        [InviteToken]   NVARCHAR(200)  NULL,
        [InvitedBy]     NVARCHAR(200)  NULL,
        [InvitedAt]     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [AcceptedAt]    DATETIME2      NULL,
        [Status]        NVARCHAR(50)   NOT NULL DEFAULT 'Invited',

        CONSTRAINT [PK_CompanyAccountants] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CompanyAccountants_Accountants] FOREIGN KEY ([AccountantId])
            REFERENCES [dbo].[Accountants] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_CompanyAccountants_AccountantId] ON [dbo].[CompanyAccountants] ([AccountantId]);
    CREATE INDEX [IX_CompanyAccountants_CompanyId] ON [dbo].[CompanyAccountants] ([CompanyId]);
    CREATE INDEX [IX_CompanyAccountants_InviteToken] ON [dbo].[CompanyAccountants] ([InviteToken]);
    CREATE UNIQUE INDEX [IX_CompanyAccountants_Company_Accountant]
        ON [dbo].[CompanyAccountants] ([CompanyId], [AccountantId]);

    PRINT 'Created table: CompanyAccountants';
END
ELSE
    PRINT 'Table CompanyAccountants already exists — skipped.';
GO

PRINT 'Accountant tables migration complete.';
