CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

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

CREATE TABLE [Faculties] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Faculties] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

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

CREATE TABLE [Modules] (
    [Id] uniqueidentifier NOT NULL,
    [SchoolId] uniqueidentifier NOT NULL,
    [FacultyId] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_Modules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Modules_Faculties_FacultyId] FOREIGN KEY ([FacultyId]) REFERENCES [Faculties] ([Id]) ON DELETE NO ACTION
);

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

CREATE INDEX [IX_AiBatches_AssessmentId] ON [AiBatches] ([AssessmentId]);

CREATE INDEX [IX_AiScripts_BatchId] ON [AiScripts] ([BatchId]);

CREATE INDEX [IX_AiScripts_Status] ON [AiScripts] ([Status]);

CREATE INDEX [IX_AiTrainingExamples_ScriptId] ON [AiTrainingExamples] ([ScriptId]);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Assessments_SchoolId_Code] ON [Assessments] ([SchoolId], [Code]);

CREATE UNIQUE INDEX [IX_Faculties_SchoolId_Code] ON [Faculties] ([SchoolId], [Code]);

CREATE INDEX [IX_Modules_FacultyId] ON [Modules] ([FacultyId]);

CREATE UNIQUE INDEX [IX_Modules_SchoolId_Code] ON [Modules] ([SchoolId], [Code]);

CREATE INDEX [IX_StaffAllocations_ModuleId] ON [StaffAllocations] ([ModuleId]);

CREATE UNIQUE INDEX [IX_StaffAllocations_SchoolId_UserId_Grade_Subject] ON [StaffAllocations] ([SchoolId], [UserId], [Grade], [Subject]) WHERE [ModuleId] IS NULL;

CREATE UNIQUE INDEX [IX_StaffAllocations_SchoolId_UserId_ModuleId] ON [StaffAllocations] ([SchoolId], [UserId], [ModuleId]) WHERE [ModuleId] IS NOT NULL;

CREATE INDEX [IX_StaffAllocations_UserId] ON [StaffAllocations] ([UserId]);

CREATE INDEX [IX_Submissions_AssessmentId] ON [Submissions] ([AssessmentId]);

CREATE INDEX [IX_Submissions_SchoolId_AssessmentId] ON [Submissions] ([SchoolId], [AssessmentId]);