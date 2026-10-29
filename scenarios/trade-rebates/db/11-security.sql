-- Logins and permissions. All application accounts are Windows (group) managed service accounts.
-- sqlcmd -v DatabaseName=TradeRebates PortalAccount="CORP\svc-rebates-web$" ApiAccount="CORP\svc-rebates-api$"
--           ActivatorAccount="CORP\svc-rebates-ea$" AnalystGroup="CORP\TR-Analysts"
-- The External Activator starts the statement worker under its own service account, so that account is the worker login.
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE master;
GO
IF SUSER_ID(N'$(PortalAccount)') IS NULL CREATE LOGIN [$(PortalAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(ApiAccount)') IS NULL CREATE LOGIN [$(ApiAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(ActivatorAccount)') IS NULL CREATE LOGIN [$(ActivatorAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'$(AnalystGroup)') IS NULL CREATE LOGIN [$(AnalystGroup)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
GO

USE [$(DatabaseName)];
GO
IF DATABASE_PRINCIPAL_ID(N'rebate_portal') IS NULL CREATE ROLE rebate_portal AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'rebate_api') IS NULL CREATE ROLE rebate_api AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'rebate_worker') IS NULL CREATE ROLE rebate_worker AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'rebate_reader') IS NULL CREATE ROLE rebate_reader AUTHORIZATION dbo;
GO

IF DATABASE_PRINCIPAL_ID(N'$(PortalAccount)') IS NULL CREATE USER [$(PortalAccount)] FOR LOGIN [$(PortalAccount)] WITH DEFAULT_SCHEMA = rebate;
IF DATABASE_PRINCIPAL_ID(N'$(ApiAccount)') IS NULL CREATE USER [$(ApiAccount)] FOR LOGIN [$(ApiAccount)] WITH DEFAULT_SCHEMA = rebate;
IF DATABASE_PRINCIPAL_ID(N'$(ActivatorAccount)') IS NULL CREATE USER [$(ActivatorAccount)] FOR LOGIN [$(ActivatorAccount)] WITH DEFAULT_SCHEMA = rebate;
IF DATABASE_PRINCIPAL_ID(N'$(AnalystGroup)') IS NULL CREATE USER [$(AnalystGroup)] FOR LOGIN [$(AnalystGroup)] WITH DEFAULT_SCHEMA = rebate;
GO

ALTER ROLE rebate_portal ADD MEMBER [$(PortalAccount)];
ALTER ROLE rebate_api ADD MEMBER [$(ApiAccount)];
ALTER ROLE rebate_worker ADD MEMBER [$(ActivatorAccount)];
ALTER ROLE rebate_reader ADD MEMBER [$(AnalystGroup)];
GO

-- Portal: everything it shows or changes goes through procedures.
GRANT EXECUTE ON rebate.Dashboard TO rebate_portal;
GRANT EXECUTE ON rebate.ListDistributors TO rebate_portal;
GRANT EXECUTE ON rebate.SaveDistributorBankAccount TO rebate_portal;
GRANT EXECUTE ON rebate.RevealDistributorBankAccount TO rebate_portal;
GRANT EXECUTE ON rebate.ListAgreements TO rebate_portal;
GRANT EXECUTE ON rebate.GetAgreement TO rebate_portal;
GRANT EXECUTE ON rebate.SaveAgreement TO rebate_portal;
GRANT EXECUTE ON rebate.SetAgreementStatus TO rebate_portal;
GRANT EXECUTE ON rebate.ListClaims TO rebate_portal;
GRANT EXECUTE ON rebate.GetClaim TO rebate_portal;
GRANT EXECUTE ON rebate.ValidateClaim TO rebate_portal;
GRANT EXECUTE ON rebate.DecideClaim TO rebate_portal;
GRANT EXECUTE ON rebate.ImportSellOutWorkbook TO rebate_portal;
GRANT EXECUTE ON rebate.CalculateAccruals TO rebate_portal;
GRANT EXECUTE ON rebate.RequestStatements TO rebate_portal;
GRANT EXECUTE ON rebate.ListStatements TO rebate_portal;
GRANT EXECUTE ON rebate.GetStatement TO rebate_portal;
GRANT EXECUTE ON rebate.ListAudit TO rebate_portal;
GRANT EXECUTE ON rebate.GetSetting TO rebate_portal;
GRANT EXECUTE ON TYPE::rebate.ClaimLineTable TO rebate_portal;
-- ImportSellOutWorkbook loads the stage table with dynamic SQL (OPENROWSET) as the caller.
GRANT SELECT, INSERT, DELETE ON SCHEMA::stage TO rebate_portal;
GO

-- Distributor API.
GRANT EXECUTE ON rebate.ResolveApiClient TO rebate_api;
GRANT EXECUTE ON rebate.SubmitClaim TO rebate_api;
GRANT EXECUTE ON rebate.GetClaim TO rebate_api;
GRANT EXECUTE ON rebate.ListDistributorClaims TO rebate_api;
GRANT EXECUTE ON rebate.ListDistributorAgreements TO rebate_api;
GRANT EXECUTE ON TYPE::rebate.ClaimLineTable TO rebate_api;
GO

-- Statement worker (started by the activator, runs as the activator account).
GRANT RECEIVE ON rebate.StatementRequestQueue TO rebate_worker;
GRANT SEND ON SERVICE::[//contosofoods.com/TradeRebates/StatementService] TO rebate_worker;
GRANT EXECUTE ON rebate.GetStatement TO rebate_worker;
GRANT EXECUTE ON rebate.CompleteStatement TO rebate_worker;
GRANT EXECUTE ON rebate.FailStatement TO rebate_worker;
GO

-- External Activator service account.
GRANT CONNECT TO [$(ActivatorAccount)];
GRANT RECEIVE ON rebate.ExternalActivatorQueue TO [$(ActivatorAccount)];
GRANT VIEW DEFINITION ON SERVICE::[//contosofoods.com/TradeRebates/ExternalActivator] TO [$(ActivatorAccount)];
GO

-- Finance analysts query the tables directly from Excel.
GRANT SELECT ON SCHEMA::rebate TO rebate_reader;
DENY SELECT ON rebate.Distributor (BankAccountProtected) TO rebate_reader;
DENY SELECT ON rebate.ApiClient TO rebate_reader;
GO
