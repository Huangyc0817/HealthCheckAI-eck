-- 1) Users add Email
IF COL_LENGTH('dbo.Users', 'Email') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD Email NVARCHAR(256) NULL;
END
GO

-- 2) Create MfaOtps table
IF OBJECT_ID('dbo.MfaOtps', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MfaOtps (
        OtpId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        OtpHash NVARCHAR(128) NOT NULL,
        ExpireAt DATETIME2 NOT NULL,
        UsedAt DATETIME2 NULL,
        FailCount INT NOT NULL DEFAULT 0,
        LockedUntil DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MfaOtps_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );

    CREATE INDEX IX_MfaOtps_UserId ON dbo.MfaOtps(UserId);
END