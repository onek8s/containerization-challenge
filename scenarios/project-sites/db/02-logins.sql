-- SQL logins for the add-in site and the sync service. Both run outside the farm and authenticate to SQL
-- with SQL logins (the add-in app pool identity is not a domain account).
-- Pass the passwords as sqlcmd variables:
--   sqlcmd -S sql01.corp.local -i 02-logins.sql -v DashboardsPassword="..." SyncPassword="..."

USE master;
GO
IF SUSER_ID(N'ps_dashboards') IS NULL
    CREATE LOGIN ps_dashboards WITH PASSWORD = N'$(DashboardsPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = ProjectSitesHub;
ELSE
    ALTER LOGIN ps_dashboards WITH PASSWORD = N'$(DashboardsPassword)';
GO
IF SUSER_ID(N'ps_sync') IS NULL
    CREATE LOGIN ps_sync WITH PASSWORD = N'$(SyncPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = ProjectSitesHub;
ELSE
    ALTER LOGIN ps_sync WITH PASSWORD = N'$(SyncPassword)';
GO

USE ProjectSitesHub;
GO
IF USER_ID(N'ps_dashboards') IS NULL CREATE USER ps_dashboards FOR LOGIN ps_dashboards WITH DEFAULT_SCHEMA = dash;
IF USER_ID(N'ps_sync') IS NULL CREATE USER ps_sync FOR LOGIN ps_sync WITH DEFAULT_SCHEMA = sync;
GO

-- Dashboards: read KPI history, manage own subscriptions and preferences, read sync status.
GRANT SELECT ON SCHEMA::dash TO ps_dashboards;
GRANT INSERT, UPDATE, DELETE ON dash.Subscription TO ps_dashboards;
GRANT INSERT, UPDATE ON dash.UserPreference TO ps_dashboards;
GRANT SELECT ON sync.Run TO ps_dashboards;

-- Sync service: owns the sync schema, writes KPI snapshots, marks digests as sent.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::sync TO ps_sync;
GRANT EXECUTE ON sync.PurgeRuns TO ps_sync;
GRANT SELECT, INSERT, UPDATE ON dash.KpiSnapshot TO ps_sync;
GRANT SELECT, UPDATE ON dash.Subscription TO ps_sync;
GO
