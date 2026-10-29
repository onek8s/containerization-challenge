/*
  Expense Claims database, logins and permissions.
  Run as a sysadmin on expenses-sql. sqlcmd variables:
    DatabaseName, AppPassword (expenses_app, used by the API), JobsPassword (expenses_jobs, used by the jobs host)
*/
:on error exit

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [$(DatabaseName)] COLLATE Finnish_Swedish_CI_AS';
    EXEC (@sql);
    ALTER DATABASE [$(DatabaseName)] SET RECOVERY FULL;
    ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'expenses_app')
    CREATE LOGIN [expenses_app] WITH PASSWORD = N'$(AppPassword)', DEFAULT_DATABASE = [$(DatabaseName)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [expenses_app] WITH PASSWORD = N'$(AppPassword)';
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'expenses_jobs')
    CREATE LOGIN [expenses_jobs] WITH PASSWORD = N'$(JobsPassword)', DEFAULT_DATABASE = [$(DatabaseName)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [expenses_jobs] WITH PASSWORD = N'$(JobsPassword)';
GO

USE [$(DatabaseName)];
GO

IF SCHEMA_ID(N'exp') IS NULL EXEC (N'CREATE SCHEMA [exp] AUTHORIZATION [dbo]');
IF SCHEMA_ID(N'quartz') IS NULL EXEC (N'CREATE SCHEMA [quartz] AUTHORIZATION [dbo]');
GO

IF DATABASE_PRINCIPAL_ID(N'expenses_app') IS NULL
    CREATE USER [expenses_app] FOR LOGIN [expenses_app] WITH DEFAULT_SCHEMA = [exp];
IF DATABASE_PRINCIPAL_ID(N'expenses_jobs') IS NULL
    CREATE USER [expenses_jobs] FOR LOGIN [expenses_jobs] WITH DEFAULT_SCHEMA = [exp];
GO

-- The API and the jobs host read and write application tables; schema changes are deployed with 03-expenseclaims-schema.sql.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[exp] TO [expenses_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[exp] TO [expenses_jobs];
-- Quartz scheduler tables (clustered job store) are used only by the jobs host.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[quartz] TO [expenses_jobs];
GO
