/*
    VanArsdelIntegration database: housekeeping procedure and the SQL Server Agent job that runs it nightly.
    The BAM data is purged by the BAM_DM_OrderLifecycle / BAM_DM_InterchangeTracking SSIS packages that
    bm.exe creates; the online window is set in Install-Bam.ps1.

    sqlcmd variables: IntegrationDatabase, JobOwner (an sa-mapped login or the Agent service account),
    RetentionDays (interchange register and duplicate keys), AuditRetentionDays (alert, login and resubmission log).
*/
SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Purge
    @RetentionDays      int = 120,
    @AuditRetentionDays int = 730,
    @BatchSize          int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cutoff datetime2(3) = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME()),
            @auditCutoff datetime2(3) = DATEADD(DAY, -@AuditRetentionDays, SYSUTCDATETIME()),
            @rows int;

    -- batches keep the log small and the pipelines' inserts unblocked
    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        DELETE TOP (@BatchSize) dbo.ReceivedInterchange WHERE ReceivedUtc < @cutoff;
        SET @rows = @@ROWCOUNT;
    END

    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        DELETE TOP (@BatchSize) dbo.Interchange WHERE CreatedUtc < @cutoff AND AckStatusId NOT IN (1, 5);
        SET @rows = @@ROWCOUNT;
    END

    -- message bodies of resubmissions are large; keep them for the interchange retention only
    DELETE b FROM dbo.ResubmitBody AS b JOIN dbo.ResubmitLog AS l ON l.ResubmitId = b.ResubmitId WHERE l.ResubmittedUtc < @cutoff;
    DELETE dbo.ResubmitLog WHERE ResubmittedUtc < @auditCutoff;
    DELETE dbo.AlertLog WHERE RaisedUtc < @auditCutoff;
    DELETE dbo.AlertState WHERE ClearedUtc < @cutoff;
    DELETE dbo.PortalLogin WHERE AtUtc < @auditCutoff;
    DELETE dbo.PartnerChangeLog WHERE ChangedUtc < @auditCutoff;
END
GO

USE msdb;
GO

DECLARE @jobId uniqueidentifier, @command nvarchar(400);
SET @command = N'EXEC dbo.usp_Purge @RetentionDays = $(RetentionDays), @AuditRetentionDays = $(AuditRetentionDays);';

SELECT @jobId = job_id FROM dbo.sysjobs WHERE name = N'B2B - Purge VanArsdelIntegration';
IF @jobId IS NOT NULL
    EXEC dbo.sp_delete_job @job_id = @jobId, @delete_unused_schedule = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.syscategories WHERE name = N'B2B Integration' AND category_class = 1)
    EXEC dbo.sp_add_category @class = N'JOB', @type = N'LOCAL', @name = N'B2B Integration';

SET @jobId = NULL;
EXEC dbo.sp_add_job
    @job_name = N'B2B - Purge VanArsdelIntegration',
    @description = N'Deletes interchange register rows, duplicate keys and logs past their retention. Deployed by Install-Database.ps1.',
    @category_name = N'B2B Integration',
    @owner_login_name = N'$(JobOwner)',
    @notify_level_eventlog = 2,
    @job_id = @jobId OUTPUT;

EXEC dbo.sp_add_jobstep
    @job_id = @jobId,
    @step_name = N'Purge',
    @subsystem = N'TSQL',
    @database_name = N'$(IntegrationDatabase)',
    @command = @command,
    @retry_attempts = 2,
    @retry_interval = 10;

-- 02:30 server time, after the BizTalk DTA purge job and before the 04:00 backups
EXEC dbo.sp_add_jobschedule
    @job_id = @jobId,
    @name = N'Nightly 02:30',
    @freq_type = 4,
    @freq_interval = 1,
    @active_start_time = 023000;

EXEC dbo.sp_add_jobserver @job_id = @jobId, @server_name = N'(local)';
GO
