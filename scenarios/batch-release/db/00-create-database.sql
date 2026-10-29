-- Creates the Batch Release database.
-- sqlcmd -S release-sql.corp.local -E -i 00-create-database.sql -v DatabaseName=BatchRelease
:on error exit
IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)];
    ALTER DATABASE [$(DatabaseName)] SET RECOVERY FULL;
    ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON;
    ALTER DATABASE [$(DatabaseName)] SET ALLOW_SNAPSHOT_ISOLATION ON;
END
GO
