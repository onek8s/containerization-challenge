-- Windows logins for the service accounts. Variables are passed by Initialize-Database.ps1:
--   $(Domain)          NetBIOS domain name, e.g. CORP
--   $(DatabaseName)    ProductReturns
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [$(DatabaseName)];
GO

IF DATABASE_PRINCIPAL_ID(N'returns_app') IS NULL CREATE ROLE returns_app;
IF DATABASE_PRINCIPAL_ID(N'returns_reader') IS NULL CREATE ROLE returns_reader;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::rtn TO returns_app;
GRANT SELECT ON SCHEMA::erp TO returns_app;
GRANT SELECT ON SCHEMA::rtn TO returns_reader;
GRANT SELECT ON SCHEMA::erp TO returns_reader;
GO

DECLARE @accounts TABLE (Account sysname, RoleName sysname);
INSERT INTO @accounts VALUES
    (N'$(Domain)\svc-returns-api',    N'returns_app'),
    (N'$(Domain)\svc-returns-labels', N'returns_app'),
    (N'$(Domain)\svc-returns-jobs',   N'returns_app'),
    (N'$(Domain)\Returns-Supervisors', N'returns_reader');

DECLARE @account sysname, @role sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Account, RoleName FROM @accounts;
OPEN c;
FETCH NEXT FROM c INTO @account, @role;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF SUSER_ID(@account) IS NULL
    BEGIN
        SET @sql = N'CREATE LOGIN ' + QUOTENAME(@account) + N' FROM WINDOWS WITH DEFAULT_DATABASE = ' + QUOTENAME(N'$(DatabaseName)') + N';';
        EXEC (@sql);
    END

    IF DATABASE_PRINCIPAL_ID(@account) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@account) + N' FOR LOGIN ' + QUOTENAME(@account) + N';';
        EXEC (@sql);
    END

    SET @sql = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@account) + N';';
    EXEC (@sql);

    FETCH NEXT FROM c INTO @account, @role;
END
CLOSE c;
DEALLOCATE c;
GO
