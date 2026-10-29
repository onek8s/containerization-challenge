-- SQL login used by the API and the sales office site (SQL Server authentication).
--   sqlcmd -S sql01.corp.local -E -i 05-login.sql -v AppLogin="fieldsales_app" AppPassword="..."
USE master;
GO
IF SUSER_ID(N'$(AppLogin)') IS NULL
    CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)', DEFAULT_DATABASE = FieldSales, CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)';
GO
USE FieldSales;
GO
IF USER_ID(N'$(AppLogin)') IS NULL
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
GO
ALTER ROLE db_datareader ADD MEMBER [$(AppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppLogin)];
GRANT EXECUTE ON SCHEMA::dbo TO [$(AppLogin)];
GRANT EXECUTE ON TYPE::dbo.OrderLineType TO [$(AppLogin)];
GRANT EXECUTE ON TYPE::dbo.PriceListRowType TO [$(AppLogin)];
GRANT EXECUTE ON TYPE::dbo.CustomerRowType TO [$(AppLogin)];
GO
