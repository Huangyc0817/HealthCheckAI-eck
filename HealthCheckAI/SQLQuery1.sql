CREATE TABLE dbo.ReportFiles (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    PatientName   NVARCHAR(100) NOT NULL,
    FileName      NVARCHAR(260) NOT NULL,   -- 存在wwwroot/uploads裡的實際檔名
    OriginalName  NVARCHAR(260) NOT NULL,   -- 原始上傳檔名
    ContentType   NVARCHAR(100) NULL,       -- MIME type (之後可用)
    UploadedAt    DATETIME2      NOT NULL DEFAULT(GETDATE()),
    ExtractedText NVARCHAR(MAX) NULL        -- 先空著，之後做文字擷取/AI 用
);