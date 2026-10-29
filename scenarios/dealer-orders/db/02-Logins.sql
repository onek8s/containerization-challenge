-- SQL logins for the applications. Passwords come from sqlcmd variables (Initialize-Database.ps1 reads them from the
-- deployment secrets file):  sqlcmd -S sql01 -i 02-Logins.sql -v PortalPassword="..." DeskPassword="..." ...
USE [master];
GO
IF SUSER_ID(N'dlr_portal') IS NULL CREATE LOGIN [dlr_portal] WITH PASSWORD = N'$(PortalPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_salesdesk') IS NULL CREATE LOGIN [dlr_salesdesk] WITH PASSWORD = N'$(DeskPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_docs') IS NULL CREATE LOGIN [dlr_docs] WITH PASSWORD = N'$(DocsPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_axconnector') IS NULL CREATE LOGIN [dlr_axconnector] WITH PASSWORD = N'$(AxConnectorPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_pricesync') IS NULL CREATE LOGIN [dlr_pricesync] WITH PASSWORD = N'$(PriceSyncPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_edi') IS NULL CREATE LOGIN [dlr_edi] WITH PASSWORD = N'$(EdiPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
IF SUSER_ID(N'dlr_notify') IS NULL CREATE LOGIN [dlr_notify] WITH PASSWORD = N'$(NotifyPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [DealerOrders];
GO

USE [DealerOrders];
GO
IF DATABASE_PRINCIPAL_ID(N'dlr_app') IS NULL CREATE ROLE [dlr_app];
IF DATABASE_PRINCIPAL_ID(N'dlr_cachewriter') IS NULL CREATE ROLE [dlr_cachewriter];
GO
-- All applications read and write the business schemas; only the sync service rewrites the cache.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[dlr] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[ax] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[edi] TO [dlr_app];
GRANT SELECT ON SCHEMA::[cache] TO [dlr_app];
-- The AX connector's status poller keeps its high-water mark in cache.SyncRun.
GRANT INSERT, UPDATE ON [cache].[SyncRun] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[cache] TO [dlr_cachewriter];
-- SqlCacheStore swaps the cache tables with SELECT INTO #staging and SqlBulkCopy (TABLOCK).
GRANT ALTER ON SCHEMA::[cache] TO [dlr_cachewriter];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[AspNetUsers] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[AspNetRoles] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[AspNetUserRoles] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[AspNetUserClaims] TO [dlr_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[AspNetUserLogins] TO [dlr_app];
GO

DECLARE @users TABLE (Name sysname);
INSERT @users VALUES (N'dlr_portal'), (N'dlr_salesdesk'), (N'dlr_docs'), (N'dlr_axconnector'), (N'dlr_pricesync'), (N'dlr_edi'), (N'dlr_notify');
DECLARE @name sysname, @sql nvarchar(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @users;
OPEN c;
FETCH NEXT FROM c INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DATABASE_PRINCIPAL_ID(@name) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@name) + N' FOR LOGIN ' + QUOTENAME(@name) + N';';
        EXEC (@sql);
    END
    SET @sql = N'ALTER ROLE [dlr_app] ADD MEMBER ' + QUOTENAME(@name) + N';';
    EXEC (@sql);
    FETCH NEXT FROM c INTO @name;
END
CLOSE c;
DEALLOCATE c;
ALTER ROLE [dlr_cachewriter] ADD MEMBER [dlr_pricesync];
GO
