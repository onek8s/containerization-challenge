-- Meter Billing database. Run with sqlcmd: sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
IF DB_ID(N'MeterBilling') IS NULL
BEGIN
    CREATE DATABASE MeterBilling;
END
GO
ALTER DATABASE MeterBilling SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
