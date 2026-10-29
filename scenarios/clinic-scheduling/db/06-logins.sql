-- Southridge Clinic Systems: SQL logins and database roles.
-- sqlcmd -i 06-logins.sql -v WebPassword="..." ServicesPassword="..." InterfacePassword="..." BatchPassword="..." PortalPassword="..." ReportsPassword="..."
USE master;
GO
IF SUSER_ID('clinic_web') IS NULL       CREATE LOGIN clinic_web       WITH PASSWORD = N'$(WebPassword)',       CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
IF SUSER_ID('clinic_services') IS NULL  CREATE LOGIN clinic_services  WITH PASSWORD = N'$(ServicesPassword)',  CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
IF SUSER_ID('clinic_interface') IS NULL CREATE LOGIN clinic_interface WITH PASSWORD = N'$(InterfacePassword)', CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
IF SUSER_ID('clinic_batch') IS NULL     CREATE LOGIN clinic_batch     WITH PASSWORD = N'$(BatchPassword)',     CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
IF SUSER_ID('clinic_portal') IS NULL    CREATE LOGIN clinic_portal    WITH PASSWORD = N'$(PortalPassword)',    CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
IF SUSER_ID('clinic_reports') IS NULL   CREATE LOGIN clinic_reports   WITH PASSWORD = N'$(ReportsPassword)',   CHECK_POLICY = OFF, DEFAULT_DATABASE = SouthridgeClinic;
GO

USE SouthridgeClinic;
GO
IF DATABASE_PRINCIPAL_ID('clinic_app') IS NULL CREATE ROLE clinic_app;
IF DATABASE_PRINCIPAL_ID('clinic_readonly') IS NULL CREATE ROLE clinic_readonly;
GO
-- The applications use LINQ to SQL and NHibernate against the tables as well as the stored procedures.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO clinic_app;
GRANT EXECUTE ON SCHEMA::dbo TO clinic_app;
GRANT SELECT ON SCHEMA::dbo TO clinic_readonly;
GRANT EXECUTE ON dbo.usp_Report_DailySchedule TO clinic_readonly;
GRANT EXECUTE ON dbo.usp_Report_ArAging TO clinic_readonly;
GRANT EXECUTE ON dbo.usp_Report_ClaimSummary TO clinic_readonly;
GRANT EXECUTE ON dbo.usp_Report_ProviderProductivity TO clinic_readonly;
GO

DECLARE @login sysname, @sql nvarchar(400);
DECLARE logins CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.server_principals WHERE name IN ('clinic_web', 'clinic_services', 'clinic_interface', 'clinic_batch', 'clinic_portal', 'clinic_reports');
OPEN logins;
FETCH NEXT FROM logins INTO @login;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DATABASE_PRINCIPAL_ID(@login) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login);
        EXEC (@sql);
    END
    SET @sql = N'EXEC sp_addrolemember ''' + CASE WHEN @login = 'clinic_reports' THEN 'clinic_readonly' ELSE 'clinic_app' END + N''', ''' + @login + N'''';
    EXEC (@sql);
    FETCH NEXT FROM logins INTO @login;
END
CLOSE logins;
DEALLOCATE logins;
GO

-- Membership, Roles and Profile (aspnet_regsql) roles for the two web sites and the WCF services.
IF DATABASE_PRINCIPAL_ID('aspnet_Membership_FullAccess') IS NOT NULL
BEGIN
    EXEC sp_addrolemember 'aspnet_Membership_FullAccess', 'clinic_web';
    EXEC sp_addrolemember 'aspnet_Roles_FullAccess', 'clinic_web';
    EXEC sp_addrolemember 'aspnet_Profile_FullAccess', 'clinic_web';
    EXEC sp_addrolemember 'aspnet_Membership_FullAccess', 'clinic_portal';
    EXEC sp_addrolemember 'aspnet_Roles_FullAccess', 'clinic_portal';
    EXEC sp_addrolemember 'aspnet_Profile_FullAccess', 'clinic_portal';
    EXEC sp_addrolemember 'aspnet_Membership_BasicAccess', 'clinic_services';
    EXEC sp_addrolemember 'aspnet_Roles_BasicAccess', 'clinic_services';
END
GO

-- Workflow persistence store used by the services site.
USE SouthridgeWorkflow;
GO
IF DATABASE_PRINCIPAL_ID('clinic_services') IS NULL CREATE USER clinic_services FOR LOGIN clinic_services;
IF DATABASE_PRINCIPAL_ID('state_persistence_users') IS NOT NULL EXEC sp_addrolemember 'state_persistence_users', 'clinic_services';
GO
