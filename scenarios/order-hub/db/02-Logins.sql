-- SQL logins for the services, the portal and the report server data source.
-- sqlcmd -v DatabaseName=OrderHub ServicePassword=... PortalPassword=... ReportsPassword=...
SET QUOTED_IDENTIFIER ON;
GO
USE [master];
GO
IF SUSER_ID(N'orderhub_svc') IS NULL
    EXEC (N'CREATE LOGIN orderhub_svc WITH PASSWORD = N''$(ServicePassword)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)]');
ELSE
    EXEC (N'ALTER LOGIN orderhub_svc WITH PASSWORD = N''$(ServicePassword)''');
IF SUSER_ID(N'orderhub_portal') IS NULL
    EXEC (N'CREATE LOGIN orderhub_portal WITH PASSWORD = N''$(PortalPassword)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)]');
ELSE
    EXEC (N'ALTER LOGIN orderhub_portal WITH PASSWORD = N''$(PortalPassword)''');
IF SUSER_ID(N'orderhub_reports') IS NULL
    EXEC (N'CREATE LOGIN orderhub_reports WITH PASSWORD = N''$(ReportsPassword)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)]');
ELSE
    EXEC (N'ALTER LOGIN orderhub_reports WITH PASSWORD = N''$(ReportsPassword)''');
GO
USE [$(DatabaseName)];
GO
IF USER_ID(N'orderhub_svc') IS NULL CREATE USER orderhub_svc FOR LOGIN orderhub_svc;
IF USER_ID(N'orderhub_portal') IS NULL CREATE USER orderhub_portal FOR LOGIN orderhub_portal;
IF USER_ID(N'orderhub_reports') IS NULL CREATE USER orderhub_reports FOR LOGIN orderhub_reports;
GO
GRANT SELECT, INSERT, UPDATE ON SCHEMA::hub TO orderhub_svc;
GRANT UPDATE ON OBJECT::hub.OrderNumbers TO orderhub_svc;
GRANT UPDATE ON OBJECT::hub.InvoiceNumbers TO orderhub_svc;
GRANT SELECT ON SCHEMA::hub TO orderhub_portal;
GRANT UPDATE ON OBJECT::hub.Products TO orderhub_portal;
GRANT INSERT ON OBJECT::hub.OrderEvents TO orderhub_portal;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO orderhub_portal;
GRANT SELECT ON OBJECT::hub.InvoiceLines TO orderhub_reports;
GO
