-- ============================================================
-- Employee Portal Migration
-- Adds: TeamMembers table, approval columns on Expenses &
--        MileageTrips, PersonalEmail on Employees,
--        mileage attachment support
-- Run against: financehub-sql-kemponline / FinanceHubDB
-- Date: 2026-03-18
-- ============================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────
-- 1. TeamMembers – links Clerk users to companies
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TeamMembers')
BEGIN
    CREATE TABLE [dbo].[TeamMembers] (
        [Id]              INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CompanyId]       INT            NOT NULL,       -- FK to CompanySettings
        [EmployeeId]      INT            NULL,           -- FK to Employees (linked after acceptance)
        [ClerkUserId]     NVARCHAR(200)  NULL,           -- Clerk user_xxx ID (set on acceptance)
        [Email]           NVARCHAR(256)  NOT NULL,       -- Invited work email
        [DisplayName]     NVARCHAR(200)  NULL,
        [Role]            NVARCHAR(50)   NOT NULL DEFAULT 'Employee',  -- Employee | Approver | Admin
        [Status]          NVARCHAR(50)   NOT NULL DEFAULT 'Invited',   -- Invited | Active | Disabled
        [InviteToken]     NVARCHAR(200)  NULL,           -- One-time invite token
        [InvitedBy]       NVARCHAR(200)  NULL,           -- Admin's Entra ObjectId
        [InvitedAt]       DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [AcceptedAt]      DATETIME2      NULL,
        [DisabledAt]      DATETIME2      NULL,
        CONSTRAINT [UQ_TeamMembers_Email_Company] UNIQUE ([Email], [CompanyId])
    );

    CREATE INDEX [IX_TeamMembers_ClerkUserId] ON [dbo].[TeamMembers] ([ClerkUserId]);
    CREATE INDEX [IX_TeamMembers_CompanyId]   ON [dbo].[TeamMembers] ([CompanyId]);
    CREATE INDEX [IX_TeamMembers_InviteToken] ON [dbo].[TeamMembers] ([InviteToken]);
    
    PRINT 'Created TeamMembers table';
END
ELSE
    PRINT 'TeamMembers table already exists';

-- ──────────────────────────────────────────────────────────
-- 2. Approval columns on Expenses
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'SubmittedByTeamMemberId')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [SubmittedByTeamMemberId] INT NULL;
    PRINT 'Added SubmittedByTeamMemberId to Expenses';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'ApprovalStatus')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [ApprovalStatus] NVARCHAR(50) NULL DEFAULT 'NotRequired';
    -- Values: NotRequired (admin-created) | Draft | Submitted | Approved | Rejected
    PRINT 'Added ApprovalStatus to Expenses';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'ApprovedByTeamMemberId')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [ApprovedByTeamMemberId] INT NULL;
    PRINT 'Added ApprovedByTeamMemberId to Expenses';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'ApprovedAt')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [ApprovedAt] DATETIME2 NULL;
    PRINT 'Added ApprovedAt to Expenses';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'RejectionReason')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [RejectionReason] NVARCHAR(500) NULL;
    PRINT 'Added RejectionReason to Expenses';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'SubmittedAt')
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [SubmittedAt] DATETIME2 NULL;
    PRINT 'Added SubmittedAt to Expenses';
END

-- ──────────────────────────────────────────────────────────
-- 3. Approval columns on MileageTrips
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'SubmittedByTeamMemberId')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [SubmittedByTeamMemberId] INT NULL;
    PRINT 'Added SubmittedByTeamMemberId to MileageTrips';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'ApprovalStatus')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [ApprovalStatus] NVARCHAR(50) NULL DEFAULT 'NotRequired';
    PRINT 'Added ApprovalStatus to MileageTrips';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'ApprovedByTeamMemberId')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [ApprovedByTeamMemberId] INT NULL;
    PRINT 'Added ApprovedByTeamMemberId to MileageTrips';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'ApprovedAt')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [ApprovedAt] DATETIME2 NULL;
    PRINT 'Added ApprovedAt to MileageTrips';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'RejectionReason')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [RejectionReason] NVARCHAR(500) NULL;
    PRINT 'Added RejectionReason to MileageTrips';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MileageTrips' AND COLUMN_NAME = 'SubmittedAt')
BEGIN
    ALTER TABLE [dbo].[MileageTrips] ADD [SubmittedAt] DATETIME2 NULL;
    PRINT 'Added SubmittedAt to MileageTrips';
END

-- ──────────────────────────────────────────────────────────
-- 4. PersonalEmail on Employees
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'PersonalEmail')
BEGIN
    ALTER TABLE [dbo].[Employees] ADD [PersonalEmail] NVARCHAR(256) NULL;
    PRINT 'Added PersonalEmail to Employees';
END

-- ──────────────────────────────────────────────────────────
-- 5. Set default ApprovalStatus for existing data
-- ──────────────────────────────────────────────────────────
EXEC sp_executesql N'UPDATE [dbo].[Expenses] SET [ApprovalStatus] = ''NotRequired'' WHERE [ApprovalStatus] IS NULL';
EXEC sp_executesql N'UPDATE [dbo].[MileageTrips] SET [ApprovalStatus] = ''NotRequired'' WHERE [ApprovalStatus] IS NULL';

PRINT 'Backfilled ApprovalStatus defaults';

COMMIT TRANSACTION;
PRINT 'Employee Portal migration completed successfully';
