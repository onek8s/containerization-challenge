-- Databases, logins and users for Broker Quotes. Run on sql01 as sysadmin:
--   sqlcmd -S sql01.corp.local -E -i 001-databases-and-logins.sql ^
--     -v QuotingPassword="..." PolicyPassword="..." DocumentsPassword="..." IdentityPassword="..."
-- The applications use SQL Server authentication (the web servers are not in the SQL Server's trust path
-- for Kerberos delegation); passwords are written into appsettings.Production.json by the deploy scripts.

SET NOCOUNT ON;

IF DB_ID(N'HumongousQuoting') IS NULL
    CREATE DATABASE HumongousQuoting;
GO
IF DB_ID(N'HumongousIdentity') IS NULL
    CREATE DATABASE HumongousIdentity;
GO
ALTER DATABASE HumongousQuoting SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

IF SUSER_ID(N'hq_quoting') IS NULL
    CREATE LOGIN hq_quoting WITH PASSWORD = N'$(QuotingPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = HumongousQuoting;
IF SUSER_ID(N'hq_policy') IS NULL
    CREATE LOGIN hq_policy WITH PASSWORD = N'$(PolicyPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = HumongousQuoting;
IF SUSER_ID(N'hq_documents') IS NULL
    CREATE LOGIN hq_documents WITH PASSWORD = N'$(DocumentsPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = HumongousQuoting;
IF SUSER_ID(N'hq_identity') IS NULL
    CREATE LOGIN hq_identity WITH PASSWORD = N'$(IdentityPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = HumongousIdentity;
GO

USE HumongousQuoting;
GO
IF USER_ID(N'hq_quoting') IS NULL CREATE USER hq_quoting FOR LOGIN hq_quoting;
IF USER_ID(N'hq_policy') IS NULL CREATE USER hq_policy FOR LOGIN hq_policy;
IF USER_ID(N'hq_documents') IS NULL CREATE USER hq_documents FOR LOGIN hq_documents;
ALTER ROLE db_datareader ADD MEMBER hq_quoting;
ALTER ROLE db_datawriter ADD MEMBER hq_quoting;
ALTER ROLE db_datareader ADD MEMBER hq_policy;
ALTER ROLE db_datawriter ADD MEMBER hq_policy;
ALTER ROLE db_datareader ADD MEMBER hq_documents;
ALTER ROLE db_datawriter ADD MEMBER hq_documents;
GO

USE HumongousIdentity;
GO
IF USER_ID(N'hq_identity') IS NULL CREATE USER hq_identity FOR LOGIN hq_identity;
ALTER ROLE db_datareader ADD MEMBER hq_identity;
ALTER ROLE db_datawriter ADD MEMBER hq_identity;
GO
