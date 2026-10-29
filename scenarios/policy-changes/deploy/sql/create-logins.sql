-- Logins for the Policy Changes accounts. Run on sql01 after create-database.sql.
-- Pass the account names as sqlcmd variables:
--   sqlcmd -S sql01.corp.local -E -i create-logins.sql -v WebAccount="CORP\svc-policychanges" DeskGroup="CORP\Policy Desk Stations"

USE [master];
GO
IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(DeskGroup)') IS NULL CREATE LOGIN [$(DeskGroup)] FROM WINDOWS;
GO

USE [PolicyChanges];
GO
IF USER_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF USER_ID(N'$(DeskGroup)') IS NULL CREATE USER [$(DeskGroup)] FOR LOGIN [$(DeskGroup)];
GO

-- Portal and partner API
ALTER ROLE [db_datareader] ADD MEMBER [$(WebAccount)];
ALTER ROLE [db_datawriter] ADD MEMBER [$(WebAccount)];
GO

-- Desk stations: read requests, update queue columns, write events, evidence and station status
GRANT SELECT ON [dbo].[ChangeRequests] TO [$(DeskGroup)];
GRANT UPDATE ON [dbo].[ChangeRequests] TO [$(DeskGroup)];
GRANT SELECT, INSERT ON [dbo].[ChangeEvents] TO [$(DeskGroup)];
GRANT SELECT, INSERT ON [dbo].[ChangeEvidence] TO [$(DeskGroup)];
GRANT SELECT, INSERT, UPDATE ON [dbo].[DeskStations] TO [$(DeskGroup)];
GRANT SELECT ON [dbo].[Partners] TO [$(DeskGroup)];
GO
