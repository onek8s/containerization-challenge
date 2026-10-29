<#
.SYNOPSIS
    Installs the intake Windows service and the scheduled tasks on the application server (inspections-app.<zone>).
    Run as administrator. Afterwards store the passwords as the service account:
        runas /user:CORP\svc-insp-jobs "C:\Program Files\Northwind\Inspections\Inspections.Jobs.exe protect-secret SharePassword"
        runas /user:CORP\svc-insp-jobs "C:\Program Files\Northwind\Inspections\Inspections.Jobs.exe protect-secret SmtpPassword"
.EXAMPLE
    .\Install-AppServer.ps1 -PublishPath ..\publish -ServiceAccount CORP\svc-insp-jobs
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$InstallPath = 'C:\Program Files\Northwind\Inspections'
)

$ErrorActionPreference = 'Stop'
$serviceName = 'NorthwindInspectionsIntake'
$credential = Get-Credential -UserName $ServiceAccount -Message 'Password for the intake service and scheduled tasks'
$password = $credential.GetNetworkCredential().Password

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

New-Item -ItemType Directory -Force -Path $InstallPath, "$env:ProgramData\Northwind\Inspections\Logs", "$env:ProgramData\Northwind\Inspections\Work" | Out-Null
foreach ($component in 'Inspections.Jobs', 'Inspections.Intake') {
    robocopy (Join-Path $PublishPath $component) $InstallPath /E /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $component failed." }
}
icacls "$env:ProgramData\Northwind\Inspections" /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

& (Join-Path $InstallPath 'Inspections.Jobs.exe') install
if ($LASTEXITCODE -ne 0) { throw 'Registering event log sources and performance counters failed.' }

# Log on as a service (intake service) and as a batch job (scheduled tasks) rights for the account.
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'secpol.cfg'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$lines = Get-Content $cfg -Encoding Unicode
$rights = foreach ($right in 'SeServiceLogonRight', 'SeBatchLogonRight') {
    $line = $lines | Where-Object { $_ -like "$right*" }
    if (-not $line) { "$right = *$sid" }
    elseif ($line -notlike "*$sid*") { "$line,*$sid" }
}
if ($rights) {
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]') + $rights | Set-Content $cfg -Encoding Unicode
    secedit /configure /db "$env:TEMP\secpol.sdb" /cfg $cfg /areas USER_RIGHTS | Out-Null
}
Remove-Item $cfg -ErrorAction SilentlyContinue

$exe = Join-Path $InstallPath 'Inspections.Intake.exe'
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $password DisplayName= 'Northwind Inspections Intake' | Out-Null
}
else {
    sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $password | Out-Null
}
sc.exe description $serviceName 'Imports inspection photos dropped in the Intake folder of the photo share.' | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
sc.exe config $serviceName depend= LanmanWorkstation | Out-Null

$jobs = Join-Path $InstallPath 'Inspections.Jobs.exe'
$tasks = @(
    @{ Name = 'Inspections - send reports'; Args = 'run-reports'; Trigger = (New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes 5)) },
    @{ Name = 'Inspections - purge photos'; Args = 'purge';       Trigger = (New-ScheduledTaskTrigger -Daily -At '02:30') }
)
foreach ($t in $tasks) {
    $action = New-ScheduledTaskAction -Execute $jobs -Argument $t.Args -WorkingDirectory $InstallPath
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2) -StartWhenAvailable
    Register-ScheduledTask -TaskName $t.Name -TaskPath '\Northwind\' -Action $action -Trigger $t.Trigger -Settings $settings `
        -User $ServiceAccount -Password $password -RunLevel Limited -Force | Out-Null
}

Write-Host "Intake service and scheduled tasks installed. Store the secrets as $ServiceAccount (see the help of this script),"
Write-Host "then start the service: Start-Service $serviceName"
