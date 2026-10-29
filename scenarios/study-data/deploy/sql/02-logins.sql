-- Logins and database roles for the Study Data service accounts.
--   sqlcmd -S sql01.corp.local -E -v Domain=CORP -i 02-logins.sql
-- svc-studydata-web: application pools of edc, monitor and sponsor-api
-- svc-studydata-job$: jobs service on trials-app01 (group managed service account)

:setvar Domain CORP

USE [master];
GO
IF SUSER_ID(N'$(Domain)\svc-studydata-web') IS NULL CREATE LOGIN [$(Domain)\svc-studydata-web] FROM WINDOWS WITH DEFAULT_DATABASE = [StudyData];
IF SUSER_ID(N'$(Domain)\svc-studydata-job$') IS NULL CREATE LOGIN [$(Domain)\svc-studydata-job$] FROM WINDOWS WITH DEFAULT_DATABASE = [StudyData];
GO

USE [StudyData];
GO
IF DATABASE_PRINCIPAL_ID(N'studydata_app') IS NULL CREATE ROLE [studydata_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[edc] TO [studydata_app];
-- The audit trail is append-only for the applications.
DENY UPDATE, DELETE ON [edc].[Audit] TO [studydata_app];
GO

IF DATABASE_PRINCIPAL_ID(N'$(Domain)\svc-studydata-web') IS NULL CREATE USER [$(Domain)\svc-studydata-web] FOR LOGIN [$(Domain)\svc-studydata-web] WITH DEFAULT_SCHEMA = [edc];
IF DATABASE_PRINCIPAL_ID(N'$(Domain)\svc-studydata-job$') IS NULL CREATE USER [$(Domain)\svc-studydata-job$] FOR LOGIN [$(Domain)\svc-studydata-job$] WITH DEFAULT_SCHEMA = [edc];
ALTER ROLE [studydata_app] ADD MEMBER [$(Domain)\svc-studydata-web];
ALTER ROLE [studydata_app] ADD MEMBER [$(Domain)\svc-studydata-job$];
GO
