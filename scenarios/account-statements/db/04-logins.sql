-- Logins and permissions. Variables (sqlcmd -v): PortalAccount, ApiAccount, DistributionAccount
-- e.g. sqlcmd -S sql01.corp.local -E -i 04-logins.sql -v PortalAccount="CORP\svc-stm-web" ApiAccount="CORP\svc-stm-api" DistributionAccount="CORP\svc-stm-dist"
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE master;
GO
IF SUSER_ID(N'$(PortalAccount)') IS NULL CREATE LOGIN [$(PortalAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(ApiAccount)') IS NULL CREATE LOGIN [$(ApiAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(DistributionAccount)') IS NULL CREATE LOGIN [$(DistributionAccount)] FROM WINDOWS;
GO
USE AccountStatements;
GO
IF DATABASE_PRINCIPAL_ID(N'stm_reader') IS NULL CREATE ROLE stm_reader;
IF DATABASE_PRINCIPAL_ID(N'stm_portal') IS NULL CREATE ROLE stm_portal;
IF DATABASE_PRINCIPAL_ID(N'stm_distribution') IS NULL CREATE ROLE stm_distribution;
GO
GRANT SELECT ON SCHEMA::stm TO stm_reader;
GRANT INSERT ON stm.DocumentRequest TO stm_reader;
GRANT UPDATE ON stm.ApiClient TO stm_reader;
GO
ALTER ROLE stm_reader ADD MEMBER stm_portal;
GRANT INSERT, UPDATE, DELETE ON stm.Subscription TO stm_portal;
GRANT INSERT, UPDATE ON stm.ApiClient TO stm_portal;
GRANT UPDATE ON stm.Delivery TO stm_portal;
GRANT UPDATE ON stm.Customer TO stm_portal;
GO
ALTER ROLE stm_reader ADD MEMBER stm_distribution;
GRANT UPDATE ON stm.Subscription TO stm_distribution;
GRANT INSERT, UPDATE ON stm.SubscriptionRun TO stm_distribution;
GRANT INSERT, UPDATE ON stm.Delivery TO stm_distribution;
GRANT EXECUTE ON stm.ClaimDueSubscriptions TO stm_distribution;
GO
IF DATABASE_PRINCIPAL_ID(N'$(PortalAccount)') IS NULL CREATE USER [$(PortalAccount)] FOR LOGIN [$(PortalAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(ApiAccount)') IS NULL CREATE USER [$(ApiAccount)] FOR LOGIN [$(ApiAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(DistributionAccount)') IS NULL CREATE USER [$(DistributionAccount)] FOR LOGIN [$(DistributionAccount)];
GO
ALTER ROLE stm_portal ADD MEMBER [$(PortalAccount)];
ALTER ROLE stm_reader ADD MEMBER [$(ApiAccount)];
ALTER ROLE stm_distribution ADD MEMBER [$(DistributionAccount)];
GO
