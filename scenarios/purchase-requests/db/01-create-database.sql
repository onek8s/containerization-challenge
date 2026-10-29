-- Run as sysadmin: sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
IF DB_ID(N'Purchasing') IS NULL
BEGIN
    CREATE DATABASE Purchasing;
END
GO
ALTER DATABASE Purchasing SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
