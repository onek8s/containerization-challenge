<#
.SYNOPSIS
    Copies Purchasing.Jobs to the application server and registers its scheduled tasks.
.EXAMPLE
    .\Install-ScheduledTasks.ps1 -PublishPath ..\publish -RunAsAccount CORP\svc-purch-jobs
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$RunAsAccount,
    [string]$InstallPath = 'C:\Apps\Purchasing.Jobs',
    [string]$TaskFolder = '\Contoso\Purchasing\'
)

$ErrorActionPreference = 'Stop'

$source = Join-Path $PublishPath 'Purchasing.Jobs'
if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
$config = Join-Path $InstallPath 'Purchasing.Jobs.exe.config'
$exclude = @('*.pdb')
if (Test-Path $config) {
    $exclude += 'Purchasing.Jobs.exe.config'
    Write-Host "Keeping existing $config"
}
robocopy $source $InstallPath /E /NFL /NDL /NJH /NJS /XF $exclude | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying Purchasing.Jobs failed.' }

$credential = Get-Credential -UserName $RunAsAccount -Message 'Password for the scheduled task account'

# Log on as a batch job right for the task account.
$sid = (New-Object System.Security.Principal.NTAccount($RunAsAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'purchasing-rights.inf'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$line = Get-Content $cfg -Encoding Unicode | Where-Object { $_ -like 'SeBatchLogonRight*' }
if (-not $line -or $line -notlike "*$sid*") {
    $new = if ($line) { "$line,*$sid" } else { "SeBatchLogonRight = *$sid" }
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $new) | Set-Content $cfg -Encoding Unicode
    secedit /configure /db (Join-Path $env:TEMP 'purchasing-rights.sdb') /cfg $cfg /areas USER_RIGHTS | Out-Null
}
Remove-Item $cfg -ErrorAction SilentlyContinue
$exe = Join-Path $InstallPath 'Purchasing.Jobs.exe'
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 30) -StartWhenAvailable

$reminders = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday, Tuesday, Wednesday, Thursday, Friday -At 07:00
Register-ScheduledTask -TaskPath $TaskFolder -TaskName 'Approval reminders' -Force `
    -Action (New-ScheduledTaskAction -Execute $exe -Argument 'reminders' -WorkingDirectory $InstallPath) `
    -Trigger $reminders -Settings $settings `
    -User $credential.UserName -Password $credential.GetNetworkCredential().Password -RunLevel Limited | Out-Null

$export = New-ScheduledTaskTrigger -Daily -At 06:00
$export.Repetition = (New-ScheduledTaskTrigger -Once -At 06:00 -RepetitionInterval (New-TimeSpan -Minutes 30) -RepetitionDuration (New-TimeSpan -Hours 14)).Repetition
Register-ScheduledTask -TaskPath $TaskFolder -TaskName 'ERP order export' -Force `
    -Action (New-ScheduledTaskAction -Execute $exe -Argument 'export' -WorkingDirectory $InstallPath) `
    -Trigger $export -Settings $settings `
    -User $credential.UserName -Password $credential.GetNetworkCredential().Password -RunLevel Limited | Out-Null

Write-Host "Scheduled tasks registered under $TaskFolder running as $RunAsAccount"
