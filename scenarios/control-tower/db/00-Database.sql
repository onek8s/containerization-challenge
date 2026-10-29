-- Control Tower database with the FILESTREAM filegroup for documents.
-- Run with sqlcmd as a sysadmin, e.g.
--   sqlcmd -S sql01.corp.local -E -i 00-Database.sql -v DataPath="E:\SQLData" LogPath="F:\SQLLogs" FileStreamPath="E:\SQLData\ControlTowerDocuments"
-- FILESTREAM must be enabled for the instance (SQL Server Configuration Manager, "Enable FILESTREAM for
-- Transact-SQL access" and "for file I/O access") before the script runs; the access level is set below.
-- The FileStreamPath folder must not exist yet; its parent must.

:setvar DatabaseName "ControlTower"
:on error exit

IF SERVERPROPERTY('FilestreamConfiguredLevel') < 2
BEGIN
    RAISERROR(N'FILESTREAM file I/O access is not enabled for this instance. Enable it in SQL Server Configuration Manager and restart the service.', 16, 1);
    RETURN;
END
GO

EXEC sp_configure N'filestream access level', 2;
RECONFIGURE;
GO

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)]
    ON PRIMARY
        (NAME = N'ControlTower', FILENAME = N'$(DataPath)\ControlTower.mdf', SIZE = 2048MB, FILEGROWTH = 512MB),
    FILEGROUP [ControlTowerData] DEFAULT
        (NAME = N'ControlTower_Data', FILENAME = N'$(DataPath)\ControlTower_Data.ndf', SIZE = 8192MB, FILEGROWTH = 1024MB),
    FILEGROUP [ControlTowerDocuments] CONTAINS FILESTREAM
        (NAME = N'ControlTower_Documents', FILENAME = N'$(FileStreamPath)')
    LOG ON
        (NAME = N'ControlTower_log', FILENAME = N'$(LogPath)\ControlTower_log.ldf', SIZE = 2048MB, FILEGROWTH = 1024MB);
END
GO

ALTER DATABASE [$(DatabaseName)] SET RECOVERY FULL;
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$(DatabaseName)] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [$(DatabaseName)] SET PAGE_VERIFY CHECKSUM;
ALTER DATABASE [$(DatabaseName)] SET FILESTREAM (NON_TRANSACTED_ACCESS = OFF);
GO

-- Separate database for the token service (ASP.NET Core Identity and IdentityServer operational store).
IF DB_ID(N'ControlTowerIdentity') IS NULL
    CREATE DATABASE [ControlTowerIdentity];
GO
ALTER DATABASE [ControlTowerIdentity] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
