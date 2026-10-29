-- Southridge Clinic Systems: SQL Server Agent jobs (run on sql01 as sysadmin).
USE msdb;
GO
DECLARE @jobs TABLE (Name sysname, Command nvarchar(max), FreqType int, FreqInterval int, SubdayType int, SubdayInterval int, StartTime int);
INSERT @jobs VALUES
 (N'Southridge - Mark no-shows',          N'EXEC dbo.usp_Appointment_MarkNoShows @AsOf = NULL;',                   4, 1, 8, 1, 90000),
 (N'Southridge - Expire referrals',       N'EXEC dbo.usp_Referral_ExpireOverdue;',                                 4, 1, 1, 0, 10000),
 (N'Southridge - Purge interface log',    N'EXEC dbo.usp_Interface_Purge @RetentionDays = 180;',                   8, 1, 1, 0, 20000),
 (N'Southridge - Index maintenance',      N'EXEC sp_MSforeachtable N''ALTER INDEX ALL ON ? REORGANIZE''; EXEC sp_updatestats;', 8, 64, 1, 0, 30000);

DECLARE @name sysname, @cmd nvarchar(max), @ft int, @fi int, @st int, @si int, @start int, @jobId uniqueidentifier;
DECLARE j CURSOR LOCAL FAST_FORWARD FOR SELECT Name, Command, FreqType, FreqInterval, SubdayType, SubdayInterval, StartTime FROM @jobs;
OPEN j;
FETCH NEXT FROM j INTO @name, @cmd, @ft, @fi, @st, @si, @start;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.sysjobs WHERE name = @name) EXEC dbo.sp_delete_job @job_name = @name;
    SET @jobId = NULL;
    EXEC dbo.sp_add_job @job_name = @name, @enabled = 1, @owner_login_name = N'sa', @category_name = N'[Uncategorized (Local)]', @job_id = @jobId OUTPUT;
    EXEC dbo.sp_add_jobstep @job_id = @jobId, @step_name = N'Run', @subsystem = N'TSQL', @database_name = N'SouthridgeClinic', @command = @cmd, @retry_attempts = 1, @retry_interval = 5;
    EXEC dbo.sp_add_jobschedule @job_id = @jobId, @name = N'Schedule', @freq_type = @ft, @freq_interval = @fi,
         @freq_subday_type = @st, @freq_subday_interval = @si, @freq_recurrence_factor = 1, @active_start_time = @start;
    EXEC dbo.sp_add_jobserver @job_id = @jobId, @server_name = N'(local)';
    FETCH NEXT FROM j INTO @name, @cmd, @ft, @fi, @st, @si, @start;
END
CLOSE j;
DEALLOCATE j;
GO
