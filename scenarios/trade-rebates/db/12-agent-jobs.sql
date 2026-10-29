-- SQL Server Agent jobs, credentials and proxies.
-- sqlcmd -v DatabaseName=TradeRebates FilesAccount="CORP\svc-rebates-files" FilesPassword="..." SsisPath="D:\TradeRebates\Ssis"
--           FilesRoot="\\rebates-files.corp.local\Rebates$" ArchiveRoot="\\rebates-files.corp.local\RebatesArchive$" JobOwner="sa"
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE master;
GO

IF EXISTS (SELECT 1 FROM sys.credentials WHERE name = N'TradeRebatesFiles')
    ALTER CREDENTIAL TradeRebatesFiles WITH IDENTITY = N'$(FilesAccount)', SECRET = N'$(FilesPassword)';
ELSE
    CREATE CREDENTIAL TradeRebatesFiles WITH IDENTITY = N'$(FilesAccount)', SECRET = N'$(FilesPassword)';
GO

USE msdb;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.sysproxies WHERE name = N'TradeRebatesFiles')
    EXEC dbo.sp_add_proxy @proxy_name = N'TradeRebatesFiles', @credential_name = N'TradeRebatesFiles', @enabled = 1,
         @description = N'File share access for Trade Rebates jobs';
GO
-- subsystem 3 = CmdExec, 11 = SSIS
IF NOT EXISTS (SELECT 1 FROM dbo.sysproxysubsystem ps JOIN dbo.sysproxies p ON p.proxy_id = ps.proxy_id WHERE p.name = N'TradeRebatesFiles' AND ps.subsystem_id = 3)
    EXEC dbo.sp_grant_proxy_to_subsystem @proxy_name = N'TradeRebatesFiles', @subsystem_id = 3;
IF NOT EXISTS (SELECT 1 FROM dbo.sysproxysubsystem ps JOIN dbo.sysproxies p ON p.proxy_id = ps.proxy_id WHERE p.name = N'TradeRebatesFiles' AND ps.subsystem_id = 11)
    EXEC dbo.sp_grant_proxy_to_subsystem @proxy_name = N'TradeRebatesFiles', @subsystem_id = 11;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.syscategories WHERE name = N'Trade Rebates' AND category_class = 1)
    EXEC dbo.sp_add_category @class = N'JOB', @type = N'LOCAL', @name = N'Trade Rebates';
GO

----------------------------------------------------------------------------------------------
-- Helper: recreate a job from scratch so the script can be re-run.
----------------------------------------------------------------------------------------------
DECLARE @jobs TABLE (name sysname);
INSERT @jobs VALUES (N'Trade Rebates - Import drop folder'), (N'Trade Rebates - Import sell-out workbooks'),
                    (N'Trade Rebates - Calculate accruals'), (N'Trade Rebates - Quarterly statements'),
                    (N'Trade Rebates - Archive processed files');
DECLARE @name sysname;
DECLARE j CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM @jobs;
OPEN j;
FETCH NEXT FROM j INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.sysjobs WHERE name = @name)
        EXEC dbo.sp_delete_job @job_name = @name, @delete_unused_schedule = 1;
    FETCH NEXT FROM j INTO @name;
END
CLOSE j;
DEALLOCATE j;
GO

----------------------------------------------------------------------------------------------
-- Every 15 minutes 06:00-22:00: distributor files from the drop folder.
----------------------------------------------------------------------------------------------
EXEC dbo.sp_add_job @job_name = N'Trade Rebates - Import drop folder', @category_name = N'Trade Rebates',
     @owner_login_name = N'$(JobOwner)', @description = N'Reads CSV and SO sell-out files from the drop folder.',
     @notify_level_eventlog = 2;
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Import drop folder', @step_name = N'Import files',
     @subsystem = N'TSQL', @database_name = N'$(DatabaseName)', @command = N'EXEC rebate.ImportDropFolder @MaxFiles = 50;',
     @retry_attempts = 1, @retry_interval = 5;
EXEC dbo.sp_add_jobschedule @job_name = N'Trade Rebates - Import drop folder', @name = N'TR every 15 minutes',
     @freq_type = 4, @freq_interval = 1, @freq_subday_type = 4, @freq_subday_interval = 15,
     @active_start_time = 060000, @active_end_time = 220000;
EXEC dbo.sp_add_jobserver @job_name = N'Trade Rebates - Import drop folder';
GO

----------------------------------------------------------------------------------------------
-- Daily 06:30: sell-out workbooks (SSIS package from the file system, runs under the file share proxy).
----------------------------------------------------------------------------------------------
EXEC dbo.sp_add_job @job_name = N'Trade Rebates - Import sell-out workbooks', @category_name = N'Trade Rebates',
     @owner_login_name = N'$(JobOwner)', @description = N'Imports .xlsx sell-out workbooks from the workbook folder.',
     @notify_level_eventlog = 2;
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Import sell-out workbooks', @step_name = N'Run SellOutWorkbookImport.dtsx',
     @subsystem = N'SSIS', @proxy_name = N'TradeRebatesFiles',
     @command = N'/FILE "\"$(SsisPath)\SellOutWorkbookImport.dtsx\"" /CHECKPOINTING OFF /REPORTING E /X86 OFF /SET "\"\Package.Variables[User::WorkbookFolder].Properties[Value]\"";"\"$(FilesRoot)\Inbound\Workbooks\"" /SET "\"\Package.Variables[User::ProcessedFolder].Properties[Value]\"";"\"$(FilesRoot)\Processed\"" /SET "\"\Package.Connections[TradeRebates].Properties[ServerName]\"";"\"$(SqlServer)\""';
EXEC dbo.sp_add_jobschedule @job_name = N'Trade Rebates - Import sell-out workbooks', @name = N'TR daily 06:30',
     @freq_type = 4, @freq_interval = 1, @active_start_time = 063000;
EXEC dbo.sp_add_jobserver @job_name = N'Trade Rebates - Import sell-out workbooks';
GO

----------------------------------------------------------------------------------------------
-- Daily 02:00: accruals for the previous and the current month.
----------------------------------------------------------------------------------------------
EXEC dbo.sp_add_job @job_name = N'Trade Rebates - Calculate accruals', @category_name = N'Trade Rebates',
     @owner_login_name = N'$(JobOwner)', @description = N'Recalculates rebate accruals.', @notify_level_eventlog = 2;
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Calculate accruals', @step_name = N'Previous month',
     @subsystem = N'TSQL', @database_name = N'$(DatabaseName)', @on_success_action = 3,
     @command = N'EXEC rebate.CalculateAccruals @Period = NULL;';
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Calculate accruals', @step_name = N'Current month',
     @subsystem = N'TSQL', @database_name = N'$(DatabaseName)',
     @command = N'DECLARE @p int = YEAR(GETDATE()) * 100 + MONTH(GETDATE()); EXEC rebate.CalculateAccruals @Period = @p;';
EXEC dbo.sp_add_jobschedule @job_name = N'Trade Rebates - Calculate accruals', @name = N'TR daily 02:00',
     @freq_type = 4, @freq_interval = 1, @active_start_time = 020000;
EXEC dbo.sp_add_jobserver @job_name = N'Trade Rebates - Calculate accruals';
GO

----------------------------------------------------------------------------------------------
-- 5th of each month 04:00; only acts in the first month of a quarter.
----------------------------------------------------------------------------------------------
EXEC dbo.sp_add_job @job_name = N'Trade Rebates - Quarterly statements', @category_name = N'Trade Rebates',
     @owner_login_name = N'$(JobOwner)', @description = N'Requests statements for the previous quarter.', @notify_level_eventlog = 2;
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Quarterly statements', @step_name = N'Request statements',
     @subsystem = N'TSQL', @database_name = N'$(DatabaseName)',
     @command = N'IF MONTH(GETDATE()) IN (1, 4, 7, 10)
BEGIN
    DECLARE @prev date = DATEADD(MONTH, -1, GETDATE());
    DECLARE @q char(6) = CAST(YEAR(@prev) AS char(4)) + ''Q'' + CAST(DATEPART(QUARTER, @prev) AS char(1));
    DECLARE @p int = YEAR(@prev) * 100 + MONTH(@prev);
    EXEC rebate.CalculateAccruals @Period = @p;
    EXEC rebate.RequestStatements @Quarter = @q, @RequestedBy = N''SQL Server Agent'';
END';
EXEC dbo.sp_add_jobschedule @job_name = N'Trade Rebates - Quarterly statements', @name = N'TR monthly day 5 04:00',
     @freq_type = 16, @freq_interval = 5, @freq_recurrence_factor = 1, @active_start_time = 040000;
EXEC dbo.sp_add_jobserver @job_name = N'Trade Rebates - Quarterly statements';
GO

----------------------------------------------------------------------------------------------
-- Sundays 03:00: processed files older than 90 days to the archive share.
----------------------------------------------------------------------------------------------
EXEC dbo.sp_add_job @job_name = N'Trade Rebates - Archive processed files', @category_name = N'Trade Rebates',
     @owner_login_name = N'$(JobOwner)', @description = N'Moves old processed files to the archive share.', @notify_level_eventlog = 2;
EXEC dbo.sp_add_jobstep @job_name = N'Trade Rebates - Archive processed files', @step_name = N'robocopy',
     @subsystem = N'CmdExec', @proxy_name = N'TradeRebatesFiles',
     -- robocopy exit codes below 8 mean success
     @command = N'cmd.exe /c robocopy "$(FilesRoot)\Processed" "$(ArchiveRoot)\Processed" /E /MOVE /MINAGE:90 /R:2 /W:30 /NP /LOG+:"$(FilesRoot)\Logs\archive.log" & if errorlevel 8 (exit /b 1) else (exit /b 0)',
     @cmdexec_success_code = 0;
EXEC dbo.sp_add_jobschedule @job_name = N'Trade Rebates - Archive processed files', @name = N'TR weekly Sunday 03:00',
     @freq_type = 8, @freq_interval = 1, @freq_recurrence_factor = 1, @active_start_time = 030000;
EXEC dbo.sp_add_jobserver @job_name = N'Trade Rebates - Archive processed files';
GO
