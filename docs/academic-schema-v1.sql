SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sp_getapplock @Resource = 'ProsperaAcademicSchemaV1', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
IF @lockResult < 0 THROW 50001, 'Could not acquire academic schema lock.', 1;
IF COL_LENGTH('dbo.Assessments', 'InstitutionJson') IS NULL
    ALTER TABLE dbo.Assessments ADD InstitutionJson nvarchar(max) NULL;
IF OBJECT_ID('dbo.Faculties') IS NULL
BEGIN
    CREATE TABLE dbo.Faculties (Id uniqueidentifier NOT NULL PRIMARY KEY, SchoolId uniqueidentifier NOT NULL, Name nvarchar(200) NOT NULL, Code nvarchar(50) NOT NULL);
    CREATE UNIQUE INDEX IX_Faculties_SchoolId_Code ON dbo.Faculties(SchoolId, Code);
END;
IF OBJECT_ID('dbo.Modules') IS NULL
BEGIN
    CREATE TABLE dbo.Modules (Id uniqueidentifier NOT NULL PRIMARY KEY, SchoolId uniqueidentifier NOT NULL, FacultyId uniqueidentifier NOT NULL, Code nvarchar(50) NOT NULL, Name nvarchar(200) NOT NULL, CONSTRAINT FK_Modules_Faculties FOREIGN KEY(FacultyId) REFERENCES dbo.Faculties(Id));
    CREATE UNIQUE INDEX IX_Modules_SchoolId_Code ON dbo.Modules(SchoolId, Code);
    CREATE INDEX IX_Modules_FacultyId ON dbo.Modules(FacultyId);
END;
IF OBJECT_ID('dbo.StaffAllocations') IS NULL
BEGIN
    CREATE TABLE dbo.StaffAllocations (Id uniqueidentifier NOT NULL PRIMARY KEY, SchoolId uniqueidentifier NOT NULL, UserId nvarchar(450) NOT NULL, Grade int NULL, Subject nvarchar(200) NULL, ModuleId uniqueidentifier NULL, CONSTRAINT FK_StaffAllocations_Users FOREIGN KEY(UserId) REFERENCES dbo.AspNetUsers(Id), CONSTRAINT FK_StaffAllocations_Modules FOREIGN KEY(ModuleId) REFERENCES dbo.Modules(Id));
    CREATE UNIQUE INDEX IX_StaffAllocations_School ON dbo.StaffAllocations(SchoolId, UserId, Grade, Subject) WHERE ModuleId IS NULL;
    CREATE UNIQUE INDEX IX_StaffAllocations_Module ON dbo.StaffAllocations(SchoolId, UserId, ModuleId) WHERE ModuleId IS NOT NULL;
    CREATE INDEX IX_StaffAllocations_UserId ON dbo.StaffAllocations(UserId);
    CREATE INDEX IX_StaffAllocations_ModuleId ON dbo.StaffAllocations(ModuleId);
END;
COMMIT TRANSACTION;
