-- Consumer Loans databases. Run as sysadmin (sqlcmd -v LendingDb=... PortalDb=...).
IF DB_ID(N'$(LendingDb)') IS NULL
BEGIN
    EXEC (N'CREATE DATABASE [$(LendingDb)]');
    EXEC (N'ALTER DATABASE [$(LendingDb)] SET READ_COMMITTED_SNAPSHOT ON');
END
GO
IF DB_ID(N'$(PortalDb)') IS NULL
    EXEC (N'CREATE DATABASE [$(PortalDb)]');
GO
