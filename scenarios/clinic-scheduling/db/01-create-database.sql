-- Southridge Clinic Systems: databases
-- Run as a sysadmin with sqlcmd: sqlcmd -S clinic-sql.corp.local -U sa -i 01-create-database.sql -v DataPath="DEFAULT" LogPath="DEFAULT"
-- (or the data and log folders of the instance, each ending with a backslash).
-- SQL Server 2008 R2 or later.

IF DB_ID(N'SouthridgeClinic') IS NULL
BEGIN
    DECLARE @sql nvarchar(max);
    SET @sql = N'CREATE DATABASE SouthridgeClinic';
    IF N'$(DataPath)' <> N'' AND N'$(DataPath)' <> N'DEFAULT'
        SET @sql = @sql + N' ON PRIMARY (NAME = SouthridgeClinic, FILENAME = ''$(DataPath)SouthridgeClinic.mdf'', SIZE = 512MB, FILEGROWTH = 256MB)'
                        + N' LOG ON (NAME = SouthridgeClinic_log, FILENAME = ''$(LogPath)SouthridgeClinic_log.ldf'', SIZE = 256MB, FILEGROWTH = 128MB)';
    EXEC (@sql);
END
GO

ALTER DATABASE SouthridgeClinic SET RECOVERY FULL;
ALTER DATABASE SouthridgeClinic SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- Windows Workflow Foundation persistence store (referral approvals). The tables and procedures are created by
-- SqlPersistenceService_Schema.sql and SqlPersistenceService_Logic.sql from the .NET Framework folder (deploy\Install-Database.ps1).
IF DB_ID(N'SouthridgeWorkflow') IS NULL
BEGIN
    DECLARE @sql2 nvarchar(max);
    SET @sql2 = N'CREATE DATABASE SouthridgeWorkflow';
    IF N'$(DataPath)' <> N'' AND N'$(DataPath)' <> N'DEFAULT'
        SET @sql2 = @sql2 + N' ON PRIMARY (NAME = SouthridgeWorkflow, FILENAME = ''$(DataPath)SouthridgeWorkflow.mdf'', SIZE = 64MB, FILEGROWTH = 64MB)'
                          + N' LOG ON (NAME = SouthridgeWorkflow_log, FILENAME = ''$(LogPath)SouthridgeWorkflow_log.ldf'', SIZE = 64MB, FILEGROWTH = 64MB)';
    EXEC (@sql2);
END
GO
