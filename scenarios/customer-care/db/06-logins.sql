-- Logins and permissions for the service accounts. Run as a sysadmin.
-- sqlcmd -S sql01.corp.local -E -i 06-logins.sql -v WebAccount="CORP\svc-care-web" ApiAccount="CORP\svc-care-api" ToolsAccount="CORP\svc-care-tools"
USE master;
GO
IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = CustomerCare;
IF SUSER_ID(N'$(ApiAccount)') IS NULL CREATE LOGIN [$(ApiAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = CustomerCare;
IF SUSER_ID(N'$(ToolsAccount)') IS NULL CREATE LOGIN [$(ToolsAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = CustomerCare;
GO
USE CustomerCare;
GO
IF DATABASE_PRINCIPAL_ID(N'care_app') IS NULL CREATE ROLE care_app;
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::care TO care_app;
GO
IF USER_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF USER_ID(N'$(ApiAccount)') IS NULL CREATE USER [$(ApiAccount)] FOR LOGIN [$(ApiAccount)];
IF USER_ID(N'$(ToolsAccount)') IS NULL CREATE USER [$(ToolsAccount)] FOR LOGIN [$(ToolsAccount)];
ALTER ROLE care_app ADD MEMBER [$(WebAccount)];
ALTER ROLE care_app ADD MEMBER [$(ApiAccount)];
ALTER ROLE care_app ADD MEMBER [$(ToolsAccount)];
GO
-- The portal keeps its background job storage in the HangFire schema and creates/upgrades the tables on start.
IF SCHEMA_ID(N'HangFire') IS NULL EXEC(N'CREATE SCHEMA HangFire AUTHORIZATION dbo');
GO
GRANT CREATE TABLE TO [$(WebAccount)];
GRANT ALTER, SELECT, INSERT, UPDATE, DELETE, EXECUTE, REFERENCES ON SCHEMA::HangFire TO [$(WebAccount)];
GO
-- The customer import loads the staging table with SqlBulkCopy.
GRANT ALTER ON care.CustomerImportStaging TO [$(ToolsAccount)];
GO
