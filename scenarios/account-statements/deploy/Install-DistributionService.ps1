<#
.SYNOPSIS
    Installs the statement distribution Windows service (WoodgroveStatementDistribution) on statements-dist.<zone>.
.EXAMPLE
    .\Install-DistributionService.ps1 -PublishPath C:\Build\AccountStatements -ServiceAccount CORP\svc-stm-dist
.EXAMPLE
    .\Install-DistributionService.ps1 -PublishPath C:\Build\AccountStatements -ServiceAccount CORP\svc-stm-dist -Settings @{ RedirectCustomerMailTo = 'test.receivables@woodgrove-leasing.fi' }
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$InstallPath = 'C:\Program Files\Woodgrove\AccountStatements\Distribution',
    [string]$DataRoot = 'D:\AccountStatements',
    [string]$SqlServer = 'sql01.corp.local',
    [string]$FileServer = 'files01.corp.local',
    [string]$SmtpServer = 'smtp.corp.local',
    [hashtable]$Settings = @{},
    [securestring]$Password
)

$ErrorActionPreference = 'Stop'
$serviceName = 'WoodgroveStatementDistribution'
$exe = Join-Path $InstallPath 'AccountStatements.Distribution.exe'

if (-not $Password) { $Password = Read-Host "Password for $ServiceAccount" -AsSecureString }
$credential = New-Object System.Management.Automation.PSCredential($ServiceAccount, $Password)

$existing = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') { Stop-Service $serviceName -Force }

New-Item -ItemType Directory -Force -Path $InstallPath, "$DataRoot\Logs", "$DataRoot\Work" | Out-Null
$configFile = "$exe.config"
$keep = Test-Path $configFile
if ($keep) { Copy-Item $configFile "$configFile.bak" -Force }
Copy-Item (Join-Path $PublishPath 'Distribution\*') $InstallPath -Recurse -Force
if ($keep) { Copy-Item "$configFile.bak" $configFile -Force }

[xml]$config = Get-Content $configFile
$cs = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AccountStatements' }
$cs.connectionString = $cs.connectionString -replace 'Data Source=[^;]+', "Data Source=$SqlServer"
$values = @{
    ArchiveRoot        = "\\$FileServer\StatementArchive$"
    StatementShareRoot = "\\$FileServer\Statements$"
    SmtpHost           = $SmtpServer
    LogFolder          = "$DataRoot\Logs"
    TempFolder         = "$DataRoot\Work"
}
foreach ($key in $Settings.Keys) { $values[$key] = [string]$Settings[$key] }
foreach ($key in $values.Keys) {
    $node = $config.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if ($node) { $node.value = $values[$key] }
    else {
        $node = $config.CreateElement('add')
        $node.SetAttribute('key', $key)
        $node.SetAttribute('value', $values[$key])
        $config.configuration.appSettings.AppendChild($node) | Out-Null
    }
}
$config.configuration.'system.net'.mailSettings.smtp.network.host = $SmtpServer
$config.Save($configFile)

icacls "$DataRoot\Logs" /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
icacls "$DataRoot\Work" /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

& $exe install-eventlog
if ($LASTEXITCODE -ne 0) { throw 'Registering the event log source failed.' }

# Log on as a service: merge the account into the existing right, keep everybody already listed.
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'stm-secpol.cfg'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$line = Get-Content $cfg -Encoding Unicode | Where-Object { $_ -like 'SeServiceLogonRight*' }
$right = $null
if (-not $line) { $right = "SeServiceLogonRight = *$sid" }
elseif ($line -notlike "*$sid*") { $right = "$line,*$sid" }
if ($right) {
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $right) | Set-Content $cfg -Encoding Unicode
    secedit /configure /db "$env:TEMP\stm-secpol.sdb" /cfg $cfg /areas USER_RIGHTS | Out-Null
}
Remove-Item $cfg -ErrorAction SilentlyContinue

$plain = $credential.GetNetworkCredential().Password
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= 'Woodgrove Statement Distribution' | Out-Null
}
else {
    sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $plain | Out-Null
}
sc.exe description $serviceName 'Runs report subscriptions and monthly customer statements: e-mail, statement share and print packs.' | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/600000 | Out-Null
sc.exe config $serviceName depend= LanmanWorkstation | Out-Null

Start-Service $serviceName
Get-Service $serviceName
