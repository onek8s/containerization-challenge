/*
    Fabrikam Housing - database
    Run as a sysadmin on sql01.corp.local (SQL Server 2017 or later).
    sqlcmd -S sql01.corp.local -E -i 01-create-database.sql -v DatabaseName=FabrikamHousing DataPath="E:\SQLData" LogPath="F:\SQLLogs"
*/
:setvar DatabaseName FabrikamHousing
:setvar DataPath "E:\SQLData"
:setvar LogPath "F:\SQLLogs"

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)]
        ON PRIMARY (NAME = N'$(DatabaseName)', FILENAME = N'$(DataPath)\$(DatabaseName).mdf', SIZE = 2GB, FILEGROWTH = 512MB)
        LOG ON (NAME = N'$(DatabaseName)_log', FILENAME = N'$(LogPath)\$(DatabaseName)_log.ldf', SIZE = 1GB, FILEGROWTH = 256MB)
        COLLATE Latin1_General_CI_AS;
END
GO

ALTER DATABASE [$(DatabaseName)] SET RECOVERY FULL;
ALTER DATABASE [$(DatabaseName)] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
