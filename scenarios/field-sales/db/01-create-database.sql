-- Creates the FieldSales database. Run with sqlcmd as a sysadmin:
--   sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
IF DB_ID(N'FieldSales') IS NULL
BEGIN
    CREATE DATABASE FieldSales;
END
GO
ALTER DATABASE FieldSales SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE FieldSales SET RECOVERY FULL;
GO
