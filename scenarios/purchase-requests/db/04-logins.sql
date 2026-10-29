-- Grants the application service accounts access to the Purchasing database.
-- sqlcmd -S sql01.corp.local -E -i 04-logins.sql -v ApiAccount="CORP\svc-purch-api" JobsAccount="CORP\svc-purch-jobs"
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(ApiAccount)')
    CREATE LOGIN [$(ApiAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = Purchasing;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(JobsAccount)')
    CREATE LOGIN [$(JobsAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = Purchasing;
GO

USE Purchasing;
GO

IF DATABASE_PRINCIPAL_ID(N'purchasing_app') IS NULL
    CREATE ROLE purchasing_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO purchasing_app;
GRANT UPDATE ON OBJECT::dbo.RequestNumbers TO purchasing_app;
GRANT UPDATE ON OBJECT::dbo.OrderNumbers TO purchasing_app;
GO

IF DATABASE_PRINCIPAL_ID(N'$(ApiAccount)') IS NULL
    CREATE USER [$(ApiAccount)] FOR LOGIN [$(ApiAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(JobsAccount)') IS NULL
    CREATE USER [$(JobsAccount)] FOR LOGIN [$(JobsAccount)];
ALTER ROLE purchasing_app ADD MEMBER [$(ApiAccount)];
ALTER ROLE purchasing_app ADD MEMBER [$(JobsAccount)];
GO
