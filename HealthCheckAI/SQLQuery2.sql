CREATE TABLE dbo.PatientFiles (
    Id INT IDENTITY(1,1) PRIMARY KEY,

    PatientName NVARCHAR(MAX) NOT NULL,
    Department NVARCHAR(MAX) NULL,

    UploadDate DATETIME2 NULL,
    UploadedAt DATETIME2 NOT NULL DEFAULT GETDATE(),

    ContentType NVARCHAR(MAX) NULL,
    ExtractedText NVARCHAR(MAX) NULL,

    AiSummary NVARCHAR(MAX) NULL,
    AiSeverity NVARCHAR(MAX) NULL,
    AiScore INT NULL,

    IsPublishedToPublic BIT NOT NULL DEFAULT 0,
    PublishedAt DATETIME2 NULL
);
