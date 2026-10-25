
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @lockResult int;
        EXEC @lockResult = sp_getapplock @Resource = 'ProsperaAiSchemaV1', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
        IF @lockResult < 0 THROW 50001, 'Could not acquire AI schema lock.', 1;
        IF OBJECT_ID('dbo.AiBatches') IS NULL
        BEGIN
          CREATE TABLE dbo.AiBatches (Id uniqueidentifier NOT NULL PRIMARY KEY, AssessmentId uniqueidentifier NOT NULL, UploadedBy nvarchar(max) NOT NULL, CreatedAt datetimeoffset NOT NULL, PaperName nvarchar(max) NOT NULL, PaperType nvarchar(max) NOT NULL, Paper varbinary(max) NOT NULL, MemoName nvarchar(max) NOT NULL, MemoType nvarchar(max) NOT NULL, Memo varbinary(max) NOT NULL, CONSTRAINT FK_AiBatches_Assessments FOREIGN KEY(AssessmentId) REFERENCES dbo.Assessments(Id));
          CREATE INDEX IX_AiBatches_AssessmentId ON dbo.AiBatches(AssessmentId);
        END;
        IF OBJECT_ID('dbo.AiScripts') IS NULL
        BEGIN
          CREATE TABLE dbo.AiScripts (Id uniqueidentifier NOT NULL PRIMARY KEY, BatchId uniqueidentifier NOT NULL, Candidate nvarchar(max) NOT NULL, FileName nvarchar(max) NOT NULL, ContentType nvarchar(max) NOT NULL, Content varbinary(max) NOT NULL, Hash nvarchar(max) NOT NULL, Status nvarchar(40) NOT NULL, UpdatedAt datetimeoffset NOT NULL, ResultJson nvarchar(max) NULL, Error nvarchar(max) NULL, Model nvarchar(max) NULL, ProviderResponseId nvarchar(max) NULL, AuditJson nvarchar(max) NOT NULL, Revision int NOT NULL, CONSTRAINT FK_AiScripts_Batches FOREIGN KEY(BatchId) REFERENCES dbo.AiBatches(Id));
          CREATE INDEX IX_AiScripts_BatchId ON dbo.AiScripts(BatchId);
          CREATE INDEX IX_AiScripts_Status ON dbo.AiScripts(Status);
        END;
        IF COL_LENGTH('dbo.AiBatches', 'ScriptLanguage') IS NULL
          ALTER TABLE dbo.AiBatches ADD ScriptLanguage nvarchar(max) NOT NULL CONSTRAINT DF_AiBatches_ScriptLanguage DEFAULT 'auto';
        IF COL_LENGTH('dbo.AiBatches', 'MemoLanguage') IS NULL
          ALTER TABLE dbo.AiBatches ADD MemoLanguage nvarchar(max) NOT NULL CONSTRAINT DF_AiBatches_MemoLanguage DEFAULT 'auto';

        IF OBJECT_ID('dbo.AiTrainingExamples') IS NULL
        BEGIN
          CREATE TABLE dbo.AiTrainingExamples (Id uniqueidentifier NOT NULL PRIMARY KEY, ScriptId uniqueidentifier NOT NULL, QuestionNumber nvarchar(max) NOT NULL, Question nvarchar(max) NOT NULL, Answer nvarchar(max) NOT NULL, Rubric nvarchar(max) NOT NULL, MaxMarks decimal(18,2) NOT NULL, Marks decimal(18,2) NOT NULL, LabelledBy nvarchar(max) NOT NULL, VerifiedBy nvarchar(max) NULL, CreatedAt datetimeoffset NOT NULL, CONSTRAINT FK_AiTrainingExamples_Scripts FOREIGN KEY(ScriptId) REFERENCES dbo.AiScripts(Id));
          CREATE INDEX IX_AiTrainingExamples_ScriptId ON dbo.AiTrainingExamples(ScriptId);
        END;
        COMMIT TRANSACTION;
