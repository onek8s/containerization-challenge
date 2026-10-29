-- Windows logins for the service accounts. Variables are passed by Initialize-Database.ps1.
USE [master];
IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(PackagerAccount)') IS NULL CREATE LOGIN [$(PackagerAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(IndexerAccount)') IS NULL CREATE LOGIN [$(IndexerAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(ReportingGroup)') IS NULL CREATE LOGIN [$(ReportingGroup)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
GO

USE [$(DatabaseName)];
IF DATABASE_PRINCIPAL_ID(N'DocControlApp') IS NULL CREATE ROLE DocControlApp;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dc TO DocControlApp;
DENY DELETE ON SCHEMA::dc TO DocControlApp;

IF DATABASE_PRINCIPAL_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(PackagerAccount)') IS NULL CREATE USER [$(PackagerAccount)] FOR LOGIN [$(PackagerAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(IndexerAccount)') IS NULL CREATE USER [$(IndexerAccount)] FOR LOGIN [$(IndexerAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(ReportingGroup)') IS NULL CREATE USER [$(ReportingGroup)] FOR LOGIN [$(ReportingGroup)];

ALTER ROLE DocControlApp ADD MEMBER [$(WebAccount)];
ALTER ROLE DocControlApp ADD MEMBER [$(PackagerAccount)];
ALTER ROLE DocControlApp ADD MEMBER [$(IndexerAccount)];
ALTER ROLE db_datareader ADD MEMBER [$(ReportingGroup)];
GO
