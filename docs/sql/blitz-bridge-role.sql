/*
  Blitz Bridge minimum-permission role script (D-4 / Batch 5 role contract)
  Run this in the target Azure SQL database (for example DBAtools).
  This script creates ONLY what Blitz Bridge requires:
  - EXECUTE on approved FRK procedures
  - VIEW DATABASE STATE
  - VIEW SERVER STATE
*/

SET NOCOUNT ON;
GO

DECLARE @RoleName sysname = N'blitz-bridge-executor';
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'blitz-bridge-executor' AND type = 'R')
BEGIN
    CREATE ROLE [blitz-bridge-executor] AUTHORIZATION [dbo];
END;
GO

GRANT VIEW DATABASE STATE TO [blitz-bridge-executor];
GO

GRANT VIEW SERVER STATE TO [blitz-bridge-executor];
GO

GRANT EXECUTE ON OBJECT::[dbo].[sp_Blitz] TO [blitz-bridge-executor];
GRANT EXECUTE ON OBJECT::[dbo].[sp_BlitzCache] TO [blitz-bridge-executor];
GRANT EXECUTE ON OBJECT::[dbo].[sp_BlitzFirst] TO [blitz-bridge-executor];
GRANT EXECUTE ON OBJECT::[dbo].[sp_BlitzIndex] TO [blitz-bridge-executor];
GRANT EXECUTE ON OBJECT::[dbo].[sp_BlitzLock] TO [blitz-bridge-executor];
GRANT EXECUTE ON OBJECT::[dbo].[sp_BlitzWho] TO [blitz-bridge-executor];
GO

/*
  Managed Identity example (Azure SQL / Entra ID):
  CREATE USER [<managed-identity-name>] FROM EXTERNAL PROVIDER;
  EXEC sp_addrolemember N'blitz-bridge-executor', N'<managed-identity-name>';
*/

/*
  SQL Authentication example:
  -- CREATE LOGIN only runs in the logical master database.
  -- CREATE LOGIN [blitz_bridge_login] WITH PASSWORD = '<strong-password>';
  CREATE USER [blitz_bridge_user] FOR LOGIN [blitz_bridge_login];
  EXEC sp_addrolemember N'blitz-bridge-executor', N'blitz_bridge_user';
*/
