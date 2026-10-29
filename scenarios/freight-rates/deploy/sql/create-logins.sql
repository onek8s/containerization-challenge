-- Run on sql01 as a sysadmin. The applications create the schema on first start
-- (the application pool account needs db_ddladmin for that first start).

IF DB_ID(N'FreightRates') IS NULL
    CREATE DATABASE FreightRates;
GO

USE FreightRates;
GO

IF SUSER_ID(N'CORP\svc-freightrates') IS NULL
    CREATE LOGIN [CORP\svc-freightrates] FROM WINDOWS WITH DEFAULT_DATABASE = FreightRates;
GO

IF USER_ID(N'svc-freightrates') IS NULL
    CREATE USER [svc-freightrates] FOR LOGIN [CORP\svc-freightrates];
GO

ALTER ROLE db_datareader ADD MEMBER [svc-freightrates];
ALTER ROLE db_datawriter ADD MEMBER [svc-freightrates];
ALTER ROLE db_ddladmin ADD MEMBER [svc-freightrates];
GO
