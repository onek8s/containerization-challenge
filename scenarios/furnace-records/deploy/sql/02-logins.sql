-- Logins and permissions. Run after 01-create-database.sql.
-- Adjust the account names with sqlcmd variables:
--   sqlcmd -S sql01.corp.local -E -i 02-logins.sql -v WebAccount="CORP\svc-furnace-web" DaqAccount="CORP\svc-furnace-daq$" WorkerAccount="CORP\svc-furnace-wrk$"
:setvar WebAccount "CORP\svc-furnace-web"
:setvar DaqAccount "CORP\svc-furnace-daq$"
:setvar WorkerAccount "CORP\svc-furnace-wrk$"

USE [master];
GO
IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(DaqAccount)') IS NULL CREATE LOGIN [$(DaqAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(WorkerAccount)') IS NULL CREATE LOGIN [$(WorkerAccount)] FROM WINDOWS;
GO

USE [FurnaceRecords];
GO
IF DATABASE_PRINCIPAL_ID(N'furnace_web') IS NULL CREATE ROLE [furnace_web];
IF DATABASE_PRINCIPAL_ID(N'furnace_daq') IS NULL CREATE ROLE [furnace_daq];
IF DATABASE_PRINCIPAL_ID(N'furnace_worker') IS NULL CREATE ROLE [furnace_worker];
GO

-- Portal and MES API (same application pool account)
GRANT SELECT ON SCHEMA::[fr] TO [furnace_web];
GRANT INSERT, UPDATE ON [fr].[Loads] TO [furnace_web];
GRANT INSERT, DELETE ON [fr].[LoadParts] TO [furnace_web];
GRANT INSERT, UPDATE ON [fr].[Recipes] TO [furnace_web];
GRANT UPDATE ON [fr].[Furnaces] TO [furnace_web];
GRANT UPDATE ON [fr].[Channels] TO [furnace_web];
GRANT INSERT, DELETE ON [fr].[Corrections] TO [furnace_web];
GRANT INSERT ON [fr].[Outbox] TO [furnace_web];
GRANT INSERT ON [fr].[ApiClients] TO [furnace_web];

-- Acquisition service
GRANT SELECT ON SCHEMA::[fr] TO [furnace_daq];
GRANT UPDATE ON [fr].[Loads] TO [furnace_daq];
GRANT INSERT ON [fr].[Samples] TO [furnace_daq];
GRANT INSERT ON [fr].[Events] TO [furnace_daq];
GRANT INSERT ON [fr].[Outbox] TO [furnace_daq];

-- Worker
GRANT SELECT ON SCHEMA::[fr] TO [furnace_worker];
GRANT UPDATE ON [fr].[Loads] TO [furnace_worker];
GRANT UPDATE ON [fr].[Outbox] TO [furnace_worker];
GRANT INSERT, UPDATE ON [fr].[JobRuns] TO [furnace_worker];
GO

IF DATABASE_PRINCIPAL_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(DaqAccount)') IS NULL CREATE USER [$(DaqAccount)] FOR LOGIN [$(DaqAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(WorkerAccount)') IS NULL CREATE USER [$(WorkerAccount)] FOR LOGIN [$(WorkerAccount)];
ALTER ROLE [furnace_web] ADD MEMBER [$(WebAccount)];
ALTER ROLE [furnace_daq] ADD MEMBER [$(DaqAccount)];
ALTER ROLE [furnace_worker] ADD MEMBER [$(WorkerAccount)];
GO
