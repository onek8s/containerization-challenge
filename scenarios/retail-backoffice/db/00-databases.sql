-- Databases and SQL logins for the retail back office (SQL01, default instance).
-- Run as sysadmin with sqlcmd; deploy\Initialize-Database.ps1 passes the passwords:
--   sqlcmd -S sql01.corp.local -i 00-databases.sql -v Environment="PROD" PwdCatalog="..." PwdPricing="..." ...
-- The services use SQL authentication; the app servers are not domain joined to the SQL tier's domain.

:on error exit
SET NOCOUNT ON;

DECLARE @dbs TABLE (Name SYSNAME, LoginName SYSNAME, Pwd NVARCHAR(128));
INSERT INTO @dbs VALUES
    (N'VanArsdelCatalog',       N'svc_catalog',       N'$(PwdCatalog)'),
    (N'VanArsdelPricing',       N'svc_pricing',       N'$(PwdPricing)'),
    (N'VanArsdelInventory',     N'svc_inventory',     N'$(PwdInventory)'),
    (N'VanArsdelAllocation',    N'svc_allocation',    N'$(PwdAllocation)'),
    (N'VanArsdelOrders',        N'svc_orders',        N'$(PwdOrders)'),
    (N'VanArsdelReturns',       N'svc_returns',       N'$(PwdReturns)'),
    (N'VanArsdelStoreOps',      N'svc_storeops',      N'$(PwdStoreOps)'),
    (N'VanArsdelErpConnector',  N'svc_erpconnector',  N'$(PwdErpConnector)'),
    (N'VanArsdelNotifications', N'svc_notifications', N'$(PwdNotifications)'),
    (N'VanArsdelReporting',     N'svc_reporting',     N'$(PwdReporting)'),
    (N'VanArsdelScheduler',     N'svc_scheduler',     N'$(PwdScheduler)');

DECLARE @name SYSNAME, @login SYSNAME, @pwd NVARCHAR(128), @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name, LoginName, Pwd FROM @dbs;
OPEN c;
FETCH NEXT FROM c INTO @name, @login, @pwd;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DB_ID(@name) IS NULL
    BEGIN
        SET @sql = N'CREATE DATABASE ' + QUOTENAME(@name) + N';';
        EXEC (@sql);
        SET @sql = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;';
        EXEC (@sql);
        SET @sql = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET RECOVERY ' + CASE WHEN N'$(Environment)' = N'PROD' THEN N'FULL' ELSE N'SIMPLE' END + N';';
        EXEC (@sql);
        PRINT 'Created database ' + @name;
    END

    IF SUSER_ID(@login) IS NULL
    BEGIN
        SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@name) + N';';
        EXEC (@sql);
        PRINT 'Created login ' + @login;
    END
    ELSE
    BEGIN
        SET @sql = N'ALTER LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N';';
        EXEC (@sql);
    END

    -- db_owner: the services create their own schema on first start in test environments (Database:AutoCreate).
    -- Production runs the db\NN-*.sql scripts and could drop to db_datareader/db_datawriter.
    SET @sql = N'USE ' + QUOTENAME(@name) + N';
        IF USER_ID(' + QUOTENAME(@login, '''') + N') IS NULL CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';
        ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@login) + N';';
    EXEC (@sql);

    FETCH NEXT FROM c INTO @name, @login, @pwd;
END
CLOSE c;
DEALLOCATE c;

-- Reporting reads nothing from the other databases; it is fed from the message bus.
GO
