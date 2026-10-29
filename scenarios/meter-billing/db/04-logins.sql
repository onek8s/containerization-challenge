-- Logins and permissions. Variables (sqlcmd -v): EngineAccount, WebAccount, OperatorsGroup
-- e.g. sqlcmd -S sql01.corp.local -E -i 04-logins.sql -v EngineAccount="CORP\svc-billing" WebAccount="CORP\svc-billing-web" OperatorsGroup="CORP\Billing-Operators"
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE master;
GO
IF SUSER_ID(N'$(EngineAccount)') IS NULL CREATE LOGIN [$(EngineAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(OperatorsGroup)') IS NULL CREATE LOGIN [$(OperatorsGroup)] FROM WINDOWS;
GO
USE MeterBilling;
GO
IF DATABASE_PRINCIPAL_ID(N'billing_engine') IS NULL CREATE ROLE billing_engine;
IF DATABASE_PRINCIPAL_ID(N'billing_reader') IS NULL CREATE ROLE billing_reader;
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::bill TO billing_engine;
GRANT SELECT ON SCHEMA::bill TO billing_reader;
GRANT INSERT ON bill.ReprintRequest TO billing_reader;
GO
IF DATABASE_PRINCIPAL_ID(N'$(EngineAccount)') IS NULL CREATE USER [$(EngineAccount)] FOR LOGIN [$(EngineAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(OperatorsGroup)') IS NULL CREATE USER [$(OperatorsGroup)] FOR LOGIN [$(OperatorsGroup)];
GO
ALTER ROLE billing_engine ADD MEMBER [$(EngineAccount)];
ALTER ROLE billing_reader ADD MEMBER [$(WebAccount)];
ALTER ROLE billing_reader ADD MEMBER [$(OperatorsGroup)];
GO
