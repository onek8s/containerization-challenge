<#
.SYNOPSIS
    Schedules the Trade Rebates database jobs with Windows Task Scheduler (for SQL Server editions without SQL Server Agent).
.DESCRIPTION
    Same schedules as db\12-agent-jobs.sql. Workbooks are imported with rebate.ImportWorkbookFolder instead of the SSIS package.
    The task account must be a Windows login on the instance with rights to run the procedures (sysadmin or db_owner)
    and Modify rights on the inbound, processed and rejected folders.
.EXAMPLE
    $cred = Get-Credential 'HOST01\svc-rebates-jobs'
    .\install-scheduled-tasks.ps1 -SqlInstance 'host01\SQLEXPRESS' -Credential $cred
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'rebates-sql.corp.local',
    [string] $DatabaseName = 'TradeRebates',
    [Parameter(Mandatory)] [pscredential] $Credential,
    [string] $FilesRoot = '\\rebates-files.corp.local\Rebates$',
    [string] $ArchiveRoot = '\\rebates-files.corp.local\RebatesArchive$',
    [string] $TaskPath = '\Contoso\TradeRebates\'
)

$ErrorActionPreference = 'Stop'
$sqlcmd = (Get-Command sqlcmd.exe).Source

function New-SqlAction([string] $Query) {
    New-ScheduledTaskAction -Execute $sqlcmd -Argument "-S `"$SqlInstance`" -E -C -b -I -d $DatabaseName -Q `"$Query`""
}

$tasks = @(
    @{ Name = 'Import drop folder'; Action = (New-SqlAction 'EXEC rebate.ImportDropFolder @MaxFiles = 50;')
       Trigger = (New-ScheduledTaskTrigger -Daily -At '06:00') ; Repeat = $true }
    @{ Name = 'Import sell-out workbooks'; Action = (New-SqlAction 'EXEC rebate.ImportWorkbookFolder;')
       Trigger = (New-ScheduledTaskTrigger -Daily -At '06:30') }
    @{ Name = 'Calculate accruals'; Action = (New-SqlAction "EXEC rebate.CalculateAccruals @Period = NULL; DECLARE @p int = YEAR(GETDATE()) * 100 + MONTH(GETDATE()); EXEC rebate.CalculateAccruals @Period = @p;")
       Trigger = (New-ScheduledTaskTrigger -Daily -At '02:00') }
    @{ Name = 'Quarterly statements'; Action = (New-SqlAction "IF DAY(GETDATE()) = 5 AND MONTH(GETDATE()) IN (1, 4, 7, 10) BEGIN DECLARE @prev date = DATEADD(MONTH, -1, GETDATE()); DECLARE @q char(6) = CAST(YEAR(@prev) AS char(4)) + 'Q' + CAST(DATEPART(QUARTER, @prev) AS char(1)); DECLARE @p int = YEAR(@prev) * 100 + MONTH(@prev); EXEC rebate.CalculateAccruals @Period = @p; EXEC rebate.RequestStatements @Quarter = @q, @RequestedBy = N'Task Scheduler'; END")
       Trigger = (New-ScheduledTaskTrigger -Daily -At '04:00') }
    @{ Name = 'Archive processed files'
       Action = (New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c robocopy `"$FilesRoot\Processed`" `"$ArchiveRoot\Processed`" /E /MOVE /MINAGE:90 /R:2 /W:30 /NP /LOG+:`"$FilesRoot\Logs\archive.log`" & if errorlevel 8 (exit /b 1) else (exit /b 0)")
       Trigger = (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '03:00') }
)

$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2)
$password = $Credential.GetNetworkCredential().Password

foreach ($t in $tasks) {
    $trigger = $t.Trigger
    if ($t.Repeat) {
        # every 15 minutes until 22:00
        $repeat = (New-ScheduledTaskTrigger -Once -At '06:00' -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration (New-TimeSpan -Hours 16)).Repetition
        $trigger.Repetition = $repeat
    }
    $name = "Trade Rebates - $($t.Name)"
    Register-ScheduledTask -TaskName $name -TaskPath $TaskPath -Action $t.Action -Trigger $trigger -Settings $settings `
        -User $Credential.UserName -Password $password -RunLevel Limited -Force | Out-Null
    Write-Host "Scheduled $TaskPath$name"
}
