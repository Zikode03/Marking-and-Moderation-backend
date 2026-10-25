using Microsoft.EntityFrameworkCore.Migrations;
namespace ProsperaAssess.EntityFrameworkCore.Migrations;

public partial class AbpBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Adopt an installed schema without deleting/recreating user tables. New databases are provisioned fully.
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.Assessments',N'U') IS NOT NULL AND COL_LENGTH('dbo.Assessments','InstitutionJson') IS NULL ALTER TABLE dbo.Assessments ADD InstitutionJson nvarchar(max) NULL;

IF OBJECT_ID(N'dbo.AiBatches',N'U') IS NOT NULL AND COL_LENGTH('dbo.AiBatches','ScriptLanguage') IS NULL ALTER TABLE dbo.AiBatches ADD ScriptLanguage nvarchar(max) NOT NULL CONSTRAINT DF_AiBatches_ScriptLanguage DEFAULT 'auto';

IF OBJECT_ID(N'dbo.AiBatches',N'U') IS NOT NULL AND COL_LENGTH('dbo.AiBatches','MemoLanguage') IS NULL ALTER TABLE dbo.AiBatches ADD MemoLanguage nvarchar(max) NOT NULL CONSTRAINT DF_AiBatches_MemoLanguage DEFAULT 'auto';

IF OBJECT_ID(N'dbo.AbpAuditLogs',N'U') IS NULL
BEGIN
CREATE TABLE [AbpAuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ApplicationName] nvarchar(96) NULL,
    [UserId] uniqueidentifier NULL,
    [UserName] nvarchar(256) NULL,
    [TenantId] uniqueidentifier NULL,
    [TenantName] nvarchar(64) NULL,
    [ImpersonatorUserId] uniqueidentifier NULL,
    [ImpersonatorUserName] nvarchar(256) NULL,
    [ImpersonatorTenantId] uniqueidentifier NULL,
    [ImpersonatorTenantName] nvarchar(64) NULL,
    [ExecutionTime] datetime2 NOT NULL,
    [ExecutionDuration] int NOT NULL,
    [ClientIpAddress] nvarchar(64) NULL,
    [ClientName] nvarchar(128) NULL,
    [ClientId] nvarchar(64) NULL,
    [CorrelationId] nvarchar(64) NULL,
    [BrowserInfo] nvarchar(512) NULL,
    [HttpMethod] nvarchar(16) NULL,
    [Url] nvarchar(256) NULL,
    [Exceptions] nvarchar(max) NULL,
    [Comments] nvarchar(256) NULL,
    [HttpStatusCode] int NULL,
    [ExtraProperties] nvarchar(max) NOT NULL,
    [ConcurrencyStamp] nvarchar(40) NOT NULL,
    CONSTRAINT [PK_AbpAuditLogs] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AbpPermissionGrants',N'U') IS NULL
BEGIN
CREATE TABLE [AbpPermissionGrants] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Name] nvarchar(128) NOT NULL,
    [ProviderName] nvarchar(64) NOT NULL,
    [ProviderKey] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_AbpPermissionGrants] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AbpPermissionGroups',N'U') IS NULL
BEGIN
CREATE TABLE [AbpPermissionGroups] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [DisplayName] nvarchar(256) NOT NULL,
    [ExtraProperties] nvarchar(max) NULL,
    CONSTRAINT [PK_AbpPermissionGroups] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AbpPermissions',N'U') IS NULL
BEGIN
CREATE TABLE [AbpPermissions] (
    [Id] uniqueidentifier NOT NULL,
    [GroupName] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [ParentName] nvarchar(128) NULL,
    [DisplayName] nvarchar(256) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [MultiTenancySide] tinyint NOT NULL,
    [Providers] nvarchar(128) NULL,
    [StateCheckers] nvarchar(256) NULL,
    [ExtraProperties] nvarchar(max) NULL,
    CONSTRAINT [PK_AbpPermissions] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AspNetRoles',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AspNetUsers',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.Assessments',N'U') IS NULL
BEGIN
CREATE TABLE [Assessments] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [CriteriaJson] nvarchar(max) NOT NULL,
    [SchoolJson] nvarchar(max) NULL,
    [InstitutionJson] nvarchar(max) NULL,
    CONSTRAINT [PK_Assessments] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.Faculties',N'U') IS NULL
BEGIN
CREATE TABLE [Faculties] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Faculties] PRIMARY KEY ([Id])
);
END;

IF OBJECT_ID(N'dbo.AbpAuditLogActions',N'U') IS NULL
BEGIN
CREATE TABLE [AbpAuditLogActions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [AuditLogId] uniqueidentifier NOT NULL,
    [ServiceName] nvarchar(256) NULL,
    [MethodName] nvarchar(128) NULL,
    [Parameters] nvarchar(2000) NULL,
    [ExecutionTime] datetime2 NOT NULL,
    [ExecutionDuration] int NOT NULL,
    [ExtraProperties] nvarchar(max) NULL,
    CONSTRAINT [PK_AbpAuditLogActions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AbpAuditLogActions_AbpAuditLogs_AuditLogId] FOREIGN KEY ([AuditLogId]) REFERENCES [AbpAuditLogs] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AbpEntityChanges',N'U') IS NULL
BEGIN
CREATE TABLE [AbpEntityChanges] (
    [Id] uniqueidentifier NOT NULL,
    [AuditLogId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [ChangeTime] datetime2 NOT NULL,
    [ChangeType] tinyint NOT NULL,
    [EntityTenantId] uniqueidentifier NULL,
    [EntityId] nvarchar(128) NULL,
    [EntityTypeFullName] nvarchar(128) NOT NULL,
    [ExtraProperties] nvarchar(max) NULL,
    CONSTRAINT [PK_AbpEntityChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AbpEntityChanges_AbpAuditLogs_AuditLogId] FOREIGN KEY ([AuditLogId]) REFERENCES [AbpAuditLogs] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AspNetRoleClaims',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AspNetUserClaims',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AspNetUserLogins',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AspNetUserRoles',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AspNetUserTokens',N'U') IS NULL
BEGIN
CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AiBatches',N'U') IS NULL
BEGIN
CREATE TABLE [AiBatches] (
    [Id] uniqueidentifier NOT NULL,
    [AssessmentId] uniqueidentifier NOT NULL,
    [UploadedBy] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [PaperName] nvarchar(max) NOT NULL,
    [PaperType] nvarchar(max) NOT NULL,
    [Paper] varbinary(max) NOT NULL,
    [MemoName] nvarchar(max) NOT NULL,
    [MemoType] nvarchar(max) NOT NULL,
    [Memo] varbinary(max) NOT NULL,
    [ScriptLanguage] nvarchar(max) NOT NULL,
    [MemoLanguage] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiBatches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiBatches_Assessments_AssessmentId] FOREIGN KEY ([AssessmentId]) REFERENCES [Assessments] ([Id]) ON DELETE NO ACTION
);
END;

IF OBJECT_ID(N'dbo.Submissions',N'U') IS NULL
BEGIN
CREATE TABLE [Submissions] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [AssessmentId] uniqueidentifier NOT NULL,
    [StudentReference] nvarchar(max) NOT NULL,
    [AnswerText] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [MarksJson] nvarchar(max) NOT NULL,
    [LastMarkerId] nvarchar(max) NULL,
    [Feedback] nvarchar(max) NULL,
    [AuditJson] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Submissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Submissions_Assessments_AssessmentId] FOREIGN KEY ([AssessmentId]) REFERENCES [Assessments] ([Id]) ON DELETE NO ACTION
);
END;

IF OBJECT_ID(N'dbo.Modules',N'U') IS NULL
BEGIN
CREATE TABLE [Modules] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [FacultyId] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_Modules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Modules_Faculties_FacultyId] FOREIGN KEY ([FacultyId]) REFERENCES [Faculties] ([Id]) ON DELETE NO ACTION
);
END;

IF OBJECT_ID(N'dbo.AbpEntityPropertyChanges',N'U') IS NULL
BEGIN
CREATE TABLE [AbpEntityPropertyChanges] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [EntityChangeId] uniqueidentifier NOT NULL,
    [NewValue] nvarchar(512) NULL,
    [OriginalValue] nvarchar(512) NULL,
    [PropertyName] nvarchar(128) NOT NULL,
    [PropertyTypeFullName] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_AbpEntityPropertyChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AbpEntityPropertyChanges_AbpEntityChanges_EntityChangeId] FOREIGN KEY ([EntityChangeId]) REFERENCES [AbpEntityChanges] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'dbo.AiScripts',N'U') IS NULL
BEGIN
CREATE TABLE [AiScripts] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [Candidate] nvarchar(max) NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [Content] varbinary(max) NOT NULL,
    [Hash] nvarchar(max) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [ResultJson] nvarchar(max) NULL,
    [Error] nvarchar(max) NULL,
    [Model] nvarchar(max) NULL,
    [ProviderResponseId] nvarchar(max) NULL,
    [AuditJson] nvarchar(max) NOT NULL,
    [Revision] int NOT NULL,
    CONSTRAINT [PK_AiScripts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiScripts_AiBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [AiBatches] ([Id]) ON DELETE NO ACTION
);
END;

IF OBJECT_ID(N'dbo.StaffAllocations',N'U') IS NULL
BEGIN
CREATE TABLE [StaffAllocations] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Grade] int NULL,
    [Subject] nvarchar(200) NULL,
    [ModuleId] uniqueidentifier NULL,
    CONSTRAINT [PK_StaffAllocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffAllocations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffAllocations_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION
);
END;

IF OBJECT_ID(N'dbo.AiTrainingExamples',N'U') IS NULL
BEGIN
CREATE TABLE [AiTrainingExamples] (
    [Id] uniqueidentifier NOT NULL,
    [ScriptId] uniqueidentifier NOT NULL,
    [QuestionNumber] nvarchar(max) NOT NULL,
    [Question] nvarchar(max) NOT NULL,
    [Answer] nvarchar(max) NOT NULL,
    [Rubric] nvarchar(max) NOT NULL,
    [MaxMarks] decimal(18,2) NOT NULL,
    [Marks] decimal(18,2) NOT NULL,
    [LabelledBy] nvarchar(max) NOT NULL,
    [VerifiedBy] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiTrainingExamples] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiTrainingExamples_AiScripts_ScriptId] FOREIGN KEY ([ScriptId]) REFERENCES [AiScripts] ([Id]) ON DELETE NO ACTION
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpAuditLogActions') AND name=N'IX_AbpAuditLogActions_AuditLogId')
CREATE INDEX [IX_AbpAuditLogActions_AuditLogId] ON [AbpAuditLogActions] ([AuditLogId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpAuditLogActions') AND name=N'IX_AbpAuditLogActions_TenantId_ServiceName_MethodName_ExecutionTime')
CREATE INDEX [IX_AbpAuditLogActions_TenantId_ServiceName_MethodName_ExecutionTime] ON [AbpAuditLogActions] ([TenantId], [ServiceName], [MethodName], [ExecutionTime]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpAuditLogs') AND name=N'IX_AbpAuditLogs_TenantId_ExecutionTime')
CREATE INDEX [IX_AbpAuditLogs_TenantId_ExecutionTime] ON [AbpAuditLogs] ([TenantId], [ExecutionTime]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpAuditLogs') AND name=N'IX_AbpAuditLogs_TenantId_UserId_ExecutionTime')
CREATE INDEX [IX_AbpAuditLogs_TenantId_UserId_ExecutionTime] ON [AbpAuditLogs] ([TenantId], [UserId], [ExecutionTime]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpEntityChanges') AND name=N'IX_AbpEntityChanges_AuditLogId')
CREATE INDEX [IX_AbpEntityChanges_AuditLogId] ON [AbpEntityChanges] ([AuditLogId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpEntityChanges') AND name=N'IX_AbpEntityChanges_TenantId_EntityTypeFullName_EntityId')
CREATE INDEX [IX_AbpEntityChanges_TenantId_EntityTypeFullName_EntityId] ON [AbpEntityChanges] ([TenantId], [EntityTypeFullName], [EntityId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpEntityPropertyChanges') AND name=N'IX_AbpEntityPropertyChanges_EntityChangeId')
CREATE INDEX [IX_AbpEntityPropertyChanges_EntityChangeId] ON [AbpEntityPropertyChanges] ([EntityChangeId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpPermissionGrants') AND name=N'IX_AbpPermissionGrants_TenantId_Name_ProviderName_ProviderKey')
CREATE UNIQUE INDEX [IX_AbpPermissionGrants_TenantId_Name_ProviderName_ProviderKey] ON [AbpPermissionGrants] ([TenantId], [Name], [ProviderName], [ProviderKey]) WHERE [TenantId] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpPermissionGroups') AND name=N'IX_AbpPermissionGroups_Name')
CREATE UNIQUE INDEX [IX_AbpPermissionGroups_Name] ON [AbpPermissionGroups] ([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpPermissions') AND name=N'IX_AbpPermissions_GroupName')
CREATE INDEX [IX_AbpPermissions_GroupName] ON [AbpPermissions] ([GroupName]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AbpPermissions') AND name=N'IX_AbpPermissions_Name')
CREATE UNIQUE INDEX [IX_AbpPermissions_Name] ON [AbpPermissions] ([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiBatches') AND name=N'IX_AiBatches_AssessmentId')
CREATE INDEX [IX_AiBatches_AssessmentId] ON [AiBatches] ([AssessmentId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiScripts') AND name=N'IX_AiScripts_BatchId')
CREATE INDEX [IX_AiScripts_BatchId] ON [AiScripts] ([BatchId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiScripts') AND name=N'IX_AiScripts_Status')
CREATE INDEX [IX_AiScripts_Status] ON [AiScripts] ([Status]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiTrainingExamples') AND name=N'IX_AiTrainingExamples_ScriptId')
CREATE INDEX [IX_AiTrainingExamples_ScriptId] ON [AiTrainingExamples] ([ScriptId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'IX_AspNetRoleClaims_RoleId')
CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'RoleNameIndex')
CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'IX_AspNetUserClaims_UserId')
CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'IX_AspNetUserLogins_UserId')
CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserRoles') AND name=N'IX_AspNetUserRoles_RoleId')
CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'EmailIndex')
CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'UserNameIndex')
CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Assessments') AND name=N'IX_Assessments_SchoolId_Code')
CREATE INDEX [IX_Assessments_SchoolId_Code] ON [Assessments] ([SchoolId], [Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Faculties') AND name=N'IX_Faculties_SchoolId_Code')
CREATE UNIQUE INDEX [IX_Faculties_SchoolId_Code] ON [Faculties] ([SchoolId], [Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Modules') AND name=N'IX_Modules_FacultyId')
CREATE INDEX [IX_Modules_FacultyId] ON [Modules] ([FacultyId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Modules') AND name=N'IX_Modules_SchoolId_Code')
CREATE UNIQUE INDEX [IX_Modules_SchoolId_Code] ON [Modules] ([SchoolId], [Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StaffAllocations') AND name=N'IX_StaffAllocations_ModuleId')
CREATE INDEX [IX_StaffAllocations_ModuleId] ON [StaffAllocations] ([ModuleId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StaffAllocations') AND name=N'IX_StaffAllocations_SchoolId_UserId_Grade_Subject')
CREATE UNIQUE INDEX [IX_StaffAllocations_SchoolId_UserId_Grade_Subject] ON [StaffAllocations] ([SchoolId], [UserId], [Grade], [Subject]) WHERE [ModuleId] IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StaffAllocations') AND name=N'IX_StaffAllocations_SchoolId_UserId_ModuleId')
CREATE UNIQUE INDEX [IX_StaffAllocations_SchoolId_UserId_ModuleId] ON [StaffAllocations] ([SchoolId], [UserId], [ModuleId]) WHERE [ModuleId] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StaffAllocations') AND name=N'IX_StaffAllocations_UserId')
CREATE INDEX [IX_StaffAllocations_UserId] ON [StaffAllocations] ([UserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Submissions') AND name=N'IX_Submissions_AssessmentId')
CREATE INDEX [IX_Submissions_AssessmentId] ON [Submissions] ([AssessmentId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Submissions') AND name=N'IX_Submissions_SchoolId_AssessmentId')
CREATE INDEX [IX_Submissions_SchoolId_AssessmentId] ON [Submissions] ([SchoolId], [AssessmentId]);
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new System.NotSupportedException("This data-preserving baseline is forward-only. Restore the verified pre-migration backup rather than dropping installed user tables.");
}
