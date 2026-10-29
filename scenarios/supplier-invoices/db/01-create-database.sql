-- Creates the Supplier Invoices database. Run with sqlcmd as a sysadmin:
--   sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
IF DB_ID(N'SupplierInvoices') IS NULL
BEGIN
    CREATE DATABASE SupplierInvoices;
END
GO
ALTER DATABASE SupplierInvoices SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
ALTER DATABASE SupplierInvoices SET RECOVERY FULL;
GO
