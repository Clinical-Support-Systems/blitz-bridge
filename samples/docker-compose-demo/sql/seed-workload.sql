SET NOCOUNT ON;

IF DB_NAME() <> N'DBAtools'
BEGIN
    RAISERROR('seed-workload.sql must run in DBAtools.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.DemoOrders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DemoOrders
    (
        OrderId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DemoOrders PRIMARY KEY,
        CustomerId int NOT NULL,
        OrderDate datetime2(0) NOT NULL,
        Amount decimal(12,2) NOT NULL,
        Region char(2) NOT NULL
    );

    CREATE INDEX IX_DemoOrders_Customer_Date ON dbo.DemoOrders(CustomerId, OrderDate DESC) INCLUDE (Amount, Region);
    CREATE INDEX IX_DemoOrders_Region_Date ON dbo.DemoOrders(Region, OrderDate DESC) INCLUDE (Amount);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.DemoOrders)
BEGIN
    ;WITH n AS
    (
        SELECT TOP (20000)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
    )
    INSERT dbo.DemoOrders (CustomerId, OrderDate, Amount, Region)
    SELECT
        (rn % 400) + 1,
        DATEADD(MINUTE, -rn, SYSUTCDATETIME()),
        CAST((rn % 997) + (rn % 37) * 0.5 AS decimal(12,2)),
        CASE rn % 5
            WHEN 0 THEN 'NE'
            WHEN 1 THEN 'SE'
            WHEN 2 THEN 'MW'
            WHEN 3 THEN 'SW'
            ELSE 'NW'
        END
    FROM n;
END;

DECLARE @i int = 0;
WHILE @i < 300
BEGIN
    DECLARE @customerId int = (@i % 400) + 1;
    DECLARE @region char(2) = CASE @i % 5 WHEN 0 THEN 'NE' WHEN 1 THEN 'SE' WHEN 2 THEN 'MW' WHEN 3 THEN 'SW' ELSE 'NW' END;

    EXEC sys.sp_executesql
        N'SELECT TOP (25) OrderId, Amount, OrderDate FROM dbo.DemoOrders WHERE CustomerId = @CustomerId ORDER BY OrderDate DESC;',
        N'@CustomerId int',
        @CustomerId = @customerId;

    EXEC sys.sp_executesql
        N'SELECT Region, COUNT_BIG(*) AS OrderCount, SUM(Amount) AS TotalAmount FROM dbo.DemoOrders WHERE Region = @Region GROUP BY Region;',
        N'@Region char(2)',
        @Region = @region;

    EXEC sys.sp_executesql
        N'SELECT TOP (10) CustomerId, SUM(Amount) AS Spend FROM dbo.DemoOrders WHERE OrderDate >= DATEADD(DAY, -7, SYSUTCDATETIME()) GROUP BY CustomerId ORDER BY Spend DESC;';

    SET @i += 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_query_stats)
BEGIN
    RAISERROR('No cached queries found after seed workload execution.', 16, 1);
END;
