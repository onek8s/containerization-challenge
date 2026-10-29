<#
.SYNOPSIS
    Installs the customer import and the case archive tools and their scheduled tasks. Run as administrator
    on the server that receives the billing extract (usually the portal server).
.EXAMPLE
    .\Install-Tools.ps1 -PublishPath ..\ -ZoneName corp.local -TaskAccount CORP\svc-care-tools
.EXAMPLE
    .\Install-Tools.ps1 -PublishPath C:\drop\CustomerCare -ZoneName corp.local -TaskAccount CORP\svc-care-tools -DataRoot E:\CustomerCare -ImportTime 01:45
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$TaskAccount,
    [string]$InstallPath = "$env:ProgramFiles\Tailspin\CustomerCare",
    [string]$DataRoot = 'D:\CustomerCare',
    [string]$ImportTime = '02:30',
    [string]$ArchiveTime = '03:00',
    [string]$BillingDropAccount
)

$ErrorActionPreference = 'Stop'

foreach ($folder in 'Import\Inbox', 'Import\Processed', 'Import\Rejected', 'Archive', 'Logs') {
    New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot $folder) | Out-Null
}
icacls $DataRoot /grant "$($TaskAccount):(OI)(CI)M" | Out-Null
if ($BillingDropAccount) {
    icacls (Join-Path $DataRoot 'Import\Inbox') /grant "$($BillingDropAccount):(OI)(CI)W" | Out-Null
    if (-not (Get-SmbShare -Name 'CareImport$' -ErrorAction SilentlyContinue)) {
        New-SmbShare -Name 'CareImport$' -Path (Join-Path $DataRoot 'Import\Inbox') -ChangeAccess $BillingDropAccount | Out-Null
    }
}

foreach ($tool in 'CustomerCare.Import', 'CustomerCare.Archive') {
    $target = Join-Path $InstallPath $tool
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    robocopy (Join-Path $PublishPath $tool) $target /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $tool failed." }

    $configPath = Join-Path $target "$tool.exe.config"
    [xml]$config = Get-Content $configPath
    foreach ($cs in $config.configuration.connectionStrings.add) { $cs.connectionString = $cs.connectionString.Replace('corp.local', $ZoneName) }
    foreach ($add in $config.configuration.appSettings.add) { $add.value = $add.value.Replace('D:\CustomerCare', $DataRoot) }
    $config.Save($configPath)
}

# The task account needs "Log on as a batch job". Add it to the accounts that already hold the right.
$sid = (New-Object Security.Principal.NTAccount($TaskAccount)).Translate([Security.Principal.SecurityIdentifier]).Value
$export = Join-Path $env:TEMP 'customercare-rights.inf'
secedit /export /cfg $export /areas USER_RIGHTS | Out-Null
$line = Get-Content $export -Encoding Unicode | Where-Object { $_ -like 'SeBatchLogonRight*' }
$current = if ($line) { ($line -split '=', 2)[1].Trim() -split ',' } else { @() }
if ($current -notcontains "*$sid") {
    $rights = (@($current | Where-Object { $_ }) + "*$sid") -join ','
    $inf = Join-Path $env:TEMP 'customercare-batch.inf'
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', "SeBatchLogonRight = $rights") |
        Set-Content $inf -Encoding Unicode
    secedit /configure /db (Join-Path $env:TEMP 'customercare-batch.sdb') /cfg $inf /areas USER_RIGHTS | Out-Null
    Remove-Item $inf
}
Remove-Item $export

$credential = Get-Credential -UserName $TaskAccount -Message 'Password for the scheduled task account'
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 3) -MultipleInstances IgnoreNew

$import = New-ScheduledTaskAction -Execute (Join-Path $InstallPath 'CustomerCare.Import\CustomerCare.Import.exe') -WorkingDirectory (Join-Path $InstallPath 'CustomerCare.Import')
Register-ScheduledTask -TaskName 'Customer Care - customer import' -TaskPath '\Tailspin\' -Action $import -Settings $settings `
    -Trigger (New-ScheduledTaskTrigger -Daily -At $ImportTime) -User $credential.UserName -Password $credential.GetNetworkCredential().Password -RunLevel Limited -Force | Out-Null

$archive = New-ScheduledTaskAction -Execute (Join-Path $InstallPath 'CustomerCare.Archive\CustomerCare.Archive.exe') -WorkingDirectory (Join-Path $InstallPath 'CustomerCare.Archive')
Register-ScheduledTask -TaskName 'Customer Care - case archive' -TaskPath '\Tailspin\' -Action $archive -Settings $settings `
    -Trigger (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At $ArchiveTime) -User $credential.UserName -Password $credential.GetNetworkCredential().Password -RunLevel Limited -Force | Out-Null

Write-Host "Tools installed to $InstallPath, data under $DataRoot."
