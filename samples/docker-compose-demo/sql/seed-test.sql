SET NOCOUNT ON;

IF DB_NAME() <> N'DBAtools'
BEGIN
    RAISERROR('seed-test.sql must run in DBAtools.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.BlitzBridgeTestSeed', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlitzBridgeTestSeed
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlitzBridgeTestSeed PRIMARY KEY,
        Marker nvarchar(100) NOT NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_BlitzBridgeTestSeed_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.BlitzBridgeTestSeed WHERE Marker = N'test-ready')
BEGIN
    INSERT dbo.BlitzBridgeTestSeed (Marker) VALUES (N'test-ready');
END;
