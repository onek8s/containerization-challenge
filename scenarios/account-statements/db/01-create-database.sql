-- Account Statements database. Run with sqlcmd: sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
IF DB_ID(N'AccountStatements') IS NULL
BEGIN
    CREATE DATABASE AccountStatements;
END
GO
ALTER DATABASE AccountStatements SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
