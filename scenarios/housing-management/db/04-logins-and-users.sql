/*
    Fabrikam Housing - logins and permissions
    The back office, jobs host and document service connect with Windows authentication as their
    group managed service accounts. The identity server, tenant portal and contractor API sit in the DMZ
    and use SQL logins (passwords are set by the deploy scripts in appsettings.Production.json).

    sqlcmd -S sql01.corp.local -E -i 04-logins-and-users.sql -v DatabaseName=FabrikamHousing
           IdentityPassword="..." PortalPassword="..." ApiPassword="..."
*/
:setvar DatabaseName FabrikamHousing

USE [master];
GO

IF SUSER_ID(N'CORP\svc-housing-web$') IS NULL CREATE LOGIN [CORP\svc-housing-web$] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'CORP\svc-housing-jobs$') IS NULL CREATE LOGIN [CORP\svc-housing-jobs$] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'CORP\svc-housing-docs$') IS NULL CREATE LOGIN [CORP\svc-housing-docs$] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'CORP\HSG-Finance') IS NULL CREATE LOGIN [CORP\HSG-Finance] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'housing_identity') IS NULL CREATE LOGIN [housing_identity] WITH PASSWORD = N'$(IdentityPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'housing_portal') IS NULL CREATE LOGIN [housing_portal] WITH PASSWORD = N'$(PortalPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
IF SUSER_ID(N'housing_api') IS NULL CREATE LOGIN [housing_api] WITH PASSWORD = N'$(ApiPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
GO

USE [$(DatabaseName)];
GO

IF DATABASE_PRINCIPAL_ID(N'housing_app') IS NULL CREATE ROLE [housing_app];
IF DATABASE_PRINCIPAL_ID(N'housing_reports') IS NULL CREATE ROLE [housing_reports];
GO
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[housing] TO [housing_app];
GRANT SELECT ON SCHEMA::[housing] TO [housing_reports];
GO

IF USER_ID(N'svc-housing-web') IS NULL CREATE USER [svc-housing-web] FOR LOGIN [CORP\svc-housing-web$];
IF USER_ID(N'svc-housing-jobs') IS NULL CREATE USER [svc-housing-jobs] FOR LOGIN [CORP\svc-housing-jobs$];
IF USER_ID(N'svc-housing-docs') IS NULL CREATE USER [svc-housing-docs] FOR LOGIN [CORP\svc-housing-docs$];
IF USER_ID(N'HSG-Finance') IS NULL CREATE USER [HSG-Finance] FOR LOGIN [CORP\HSG-Finance];
IF USER_ID(N'housing_identity') IS NULL CREATE USER [housing_identity] FOR LOGIN [housing_identity];
IF USER_ID(N'housing_portal') IS NULL CREATE USER [housing_portal] FOR LOGIN [housing_portal];
IF USER_ID(N'housing_api') IS NULL CREATE USER [housing_api] FOR LOGIN [housing_api];
GO

ALTER ROLE [housing_app] ADD MEMBER [svc-housing-web];
ALTER ROLE [housing_app] ADD MEMBER [svc-housing-jobs];
ALTER ROLE [housing_app] ADD MEMBER [svc-housing-docs];
ALTER ROLE [housing_app] ADD MEMBER [housing_portal];
ALTER ROLE [housing_app] ADD MEMBER [housing_api];
ALTER ROLE [housing_reports] ADD MEMBER [HSG-Finance];
GO

-- The identity server only reads tenants and contractors from the housing schema and owns the auth schema.
GRANT SELECT ON housing.Tenants TO [housing_identity];
GRANT SELECT ON housing.Tenancies TO [housing_identity];
GRANT SELECT ON housing.Properties TO [housing_identity];
GRANT SELECT ON housing.Contractors TO [housing_identity];
GRANT UPDATE ON housing.Tenants (PortalSubjectId) TO [housing_identity];
GRANT INSERT ON housing.ErrorLog TO [housing_identity];
GRANT INSERT ON housing.AuditEntries TO [housing_identity];
IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA [auth] AUTHORIZATION [dbo];');
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth] TO [housing_identity];
GO

-- Hangfire creates and upgrades its own tables in the HangFire schema on start-up.
IF SCHEMA_ID(N'HangFire') IS NULL EXEC(N'CREATE SCHEMA [HangFire] AUTHORIZATION [svc-housing-jobs];');
GRANT CREATE TABLE TO [svc-housing-jobs];
GO
