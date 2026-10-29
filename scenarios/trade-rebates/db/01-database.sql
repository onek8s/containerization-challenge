-- Trade Rebates database. Run with sqlcmd -v DatabaseName=TradeRebates DataPath="D:\SQLData" LogPath="L:\SQLLogs"
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [$(DatabaseName)]
        ON PRIMARY (NAME = N''$(DatabaseName)'', FILENAME = N''$(DataPath)\$(DatabaseName).mdf'', SIZE = 512MB, FILEGROWTH = 256MB)
        LOG ON (NAME = N''$(DatabaseName)_log'', FILENAME = N''$(LogPath)\$(DatabaseName)_log.ldf'', SIZE = 256MB, FILEGROWTH = 128MB)';
    EXEC (@sql);
END
GO

ALTER DATABASE [$(DatabaseName)] SET RECOVERY FULL;
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- Statement runs are queued with Service Broker.
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'$(DatabaseName)' AND is_broker_enabled = 1)
    ALTER DATABASE [$(DatabaseName)] SET ENABLE_BROKER WITH ROLLBACK IMMEDIATE;
GO

ALTER AUTHORIZATION ON DATABASE::[$(DatabaseName)] TO [sa];
GO
