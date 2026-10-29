-- SQL Agent job: nightly purge of the sync run history. Requires SQL Server Agent (not available on Express).
USE msdb;
GO
IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'ProjectSitesHub - purge sync runs')
BEGIN
    EXEC dbo.sp_add_job @job_name = N'ProjectSitesHub - purge sync runs', @owner_login_name = N'sa';
    EXEC dbo.sp_add_jobstep @job_name = N'ProjectSitesHub - purge sync runs', @step_name = N'Purge',
        @subsystem = N'TSQL', @database_name = N'ProjectSitesHub', @command = N'EXEC sync.PurgeRuns @Days = 180;';
    EXEC dbo.sp_add_schedule @schedule_name = N'ProjectSitesHub nightly 02:30', @freq_type = 4, @freq_interval = 1, @active_start_time = 023000;
    EXEC dbo.sp_attach_schedule @job_name = N'ProjectSitesHub - purge sync runs', @schedule_name = N'ProjectSitesHub nightly 02:30';
    EXEC dbo.sp_add_jobserver @job_name = N'ProjectSitesHub - purge sync runs';
END
GO
