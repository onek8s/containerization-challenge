<#
.SYNOPSIS
    Installs ProductReturns.Jobs and registers its scheduled tasks on returns-jobs.
    The .NET Core 3.1 runtime must be installed first.
.EXAMPLE
    .\Register-ReturnsJobs.ps1 -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-jobs
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$ZoneName = 'corp.local',
    [string]$SqlServer = "sql01.$ZoneName",
    [string]$FileServer = "files01.$ZoneName",
    [string]$InstallRoot = 'C:\Program Files\Wide World Importers\Product Returns',
    [string]$DataRoot = 'D:\ReturnsData'
)

$ErrorActionPreference = 'Stop'

$installPath = Join-Path $InstallRoot 'ProductReturns.Jobs'
New-Item -ItemType Directory -Force -Path $installPath, "$DataRoot\Jobs\Logs", "$DataRoot\Jobs\Archive" | Out-Null
robocopy (Join-Path $PublishPath 'ProductReturns.Jobs') $installPath /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying ProductReturns.Jobs failed.' }
icacls $DataRoot /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

$settingsFolder = 'C:\ProgramData\WideWorldImporters\ProductReturns'
New-Item -ItemType Directory -Force -Path $settingsFolder | Out-Null
@{
    ConnectionStrings = @{ Returns = "Server=$SqlServer;Database=ProductReturns;Integrated Security=true;Application Name=ProductReturns.Jobs" }
    Jobs = @{
        RefundOutbox = "\\$FileServer\Finance$\Refunds\Inbound"
        LabelShare   = "\\$FileServer\Returns$\Labels"
        StateFolder  = "$DataRoot\Jobs"
        LogFolder    = "$DataRoot\Jobs\Logs"
        RefundArchive = "$DataRoot\Jobs\Archive"
    }
    Mail = @{ SmtpHost = "smtp.$ZoneName" }
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $settingsFolder 'jobs.settings.json') -Encoding UTF8

if (-not [System.Diagnostics.EventLog]::SourceExists('Product Returns Jobs')) {
    New-EventLog -LogName Application -Source 'Product Returns Jobs'
}

# Log on as a batch job right.
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'secpol.cfg'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$lines = Get-Content $cfg -Encoding Unicode
$line = $lines | Where-Object { $_ -like 'SeBatchLogonRight*' }
if (-not $line) {
    $lines = $lines -replace '^\[Privilege Rights\]$', "[Privilege Rights]`r`nSeBatchLogonRight = *$sid"
}
elseif ($line -notlike "*$sid*") {
    $lines = $lines -replace [regex]::Escape($line), "$line,*$sid"
}
Set-Content $cfg $lines -Encoding Unicode
secedit /configure /db "$env:TEMP\secpol.sdb" /cfg $cfg /areas USER_RIGHTS | Out-Null
Remove-Item $cfg -ErrorAction SilentlyContinue

$credential = Get-Credential -UserName $ServiceAccount -Message 'Password for the scheduled tasks'
$exe = Join-Path $installPath 'ProductReturns.Jobs.exe'
$weekdays = 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'

$tasks = @(
    @{ Name = 'Refund export';  Verb = 'refunds';   Trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $weekdays -At '06:30' }
    @{ Name = 'Reminders';      Verb = 'reminders'; Trigger = New-ScheduledTaskTrigger -Daily -At '09:00' }
    @{ Name = 'Expire returns'; Verb = 'expire';    Trigger = New-ScheduledTaskTrigger -Daily -At '02:00' }
    @{ Name = 'Purge';          Verb = 'purge';     Trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '03:00' }
)

foreach ($task in $tasks) {
    $action = New-ScheduledTaskAction -Execute $exe -Argument $task.Verb -WorkingDirectory $installPath
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew -StartWhenAvailable
    Register-ScheduledTask -TaskPath '\Wide World Importers\Product Returns\' -TaskName $task.Name -Action $action -Trigger $task.Trigger `
        -Settings $settings -User $ServiceAccount -Password $credential.GetNetworkCredential().Password -RunLevel Limited -Force | Out-Null
    Write-Host "Scheduled task '$($task.Name)' registered ($($task.Verb))"
}
