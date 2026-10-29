<#
.SYNOPSIS
    Installs the billing engine Windows service (CohoMeterBilling) on billing-engine.
    Creates the log folder and the BillingLogs$ share read by Billing Monitor, reserves the
    control endpoint URL for the service account and opens the firewall port.
.EXAMPLE
    .\Install-EngineService.ps1 -PublishPath C:\Build\MeterBilling -ServiceAccount CORP\svc-billing -OperatorsGroup CORP\Billing-Operators
.EXAMPLE
    .\Install-EngineService.ps1 -PublishPath C:\Build\MeterBilling -ServiceAccount CORP\svc-billing -OperatorsGroup CORP\Billing-Operators -Settings @{ AutoReleaseLimit = '0' }
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [Parameter(Mandatory)] [string]$OperatorsGroup,
    [string]$InstallPath = 'C:\Program Files\Coho\MeterBilling\Engine',
    [string]$LogFolder = 'D:\MeterBilling\Logs',
    [string]$SqlServer = 'sql01.corp.local',
    [string]$FileServer = 'files01.corp.local',
    [int]$ControlPort = 8441,
    [string]$ReprintCallers = 'CORP\svc-billing-web',
    [hashtable]$Settings = @{},
    [securestring]$Password
)

$ErrorActionPreference = 'Stop'
$serviceName = 'CohoMeterBilling'
$exe = Join-Path $InstallPath 'MeterBilling.Engine.exe'

if (-not $Password) { $Password = Read-Host "Password for $ServiceAccount" -AsSecureString }
$credential = New-Object System.Management.Automation.PSCredential($ServiceAccount, $Password)

# Stop and copy binaries
$existing = Get-Service $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') { Stop-Service $serviceName -Force }

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
$configFile = "$exe.config"
$keepConfig = Test-Path $configFile
if ($keepConfig) { Copy-Item $configFile "$configFile.bak" -Force }
Copy-Item (Join-Path $PublishPath 'Engine\*') $InstallPath -Recurse -Force
if ($keepConfig) { Copy-Item "$configFile.bak" $configFile -Force }

# Configuration
[xml]$config = Get-Content $configFile
$values = @{
    ReadInbox      = "\\$FileServer\MeterReads$\Inbox"
    ReadArchive    = "\\$FileServer\MeterReads$\Processed"
    ReadRejected   = "\\$FileServer\MeterReads$\Rejected"
    PrintOutput    = "\\$FileServer\BillingOut$\Print"
    BankOutput     = "\\$FileServer\BillingOut$\Bank"
    LogFolder      = $LogFolder
    ControlUrl     = "http://+:$ControlPort/"
    OperatorsGroup = $OperatorsGroup
    ReprintCallers = $ReprintCallers
}
foreach ($key in $Settings.Keys) { $values[$key] = [string]$Settings[$key] }
foreach ($key in $values.Keys) {
    $node = $config.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if ($node) { $node.value = $values[$key] }
    else {
        $add = $config.CreateElement('add'); $add.SetAttribute('key', $key); $add.SetAttribute('value', $values[$key])
        $config.configuration.appSettings.AppendChild($add) | Out-Null
    }
}
$cs = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'MeterBilling' }
$cs.connectionString = "Data Source=$SqlServer;Initial Catalog=MeterBilling;Integrated Security=SSPI;Application Name=MeterBilling.Engine"
$config.Save($configFile)

# Log folder and share for Billing Monitor
New-Item -ItemType Directory -Path $LogFolder -Force | Out-Null
& icacls $LogFolder /grant "${ServiceAccount}:(OI)(CI)M" "${OperatorsGroup}:(OI)(CI)RX" | Out-Null
if (-not (Get-SmbShare -Name 'BillingLogs$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'BillingLogs$' -Path $LogFolder -ReadAccess $OperatorsGroup | Out-Null
}

# Event source, URL reservation and firewall
& $exe install-eventlog
& netsh http delete urlacl url="http://+:$ControlPort/" 2>$null | Out-Null
& netsh http add urlacl url="http://+:$ControlPort/" user="$ServiceAccount"
if (-not (Get-NetFirewallRule -Name 'CohoMeterBillingControl' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'CohoMeterBillingControl' -DisplayName 'Coho Meter Billing control endpoint' -Direction Inbound -Protocol TCP -LocalPort $ControlPort -Action Allow | Out-Null
}

# Log on as a service right
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$temp = Join-Path $env:TEMP 'billing-secpol'
New-Item -ItemType Directory -Path $temp -Force | Out-Null
& secedit /export /cfg "$temp\current.inf" /areas USER_RIGHTS | Out-Null
$line = (Get-Content "$temp\current.inf") | Where-Object { $_ -like 'SeServiceLogonRight*' }
if ($line -notmatch [regex]::Escape($sid)) {
    $value = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $value) | Set-Content "$temp\grant.inf" -Encoding Unicode
    & secedit /configure /db "$temp\grant.sdb" /cfg "$temp\grant.inf" /areas USER_RIGHTS | Out-Null
}
Remove-Item $temp -Recurse -Force

# Service
if (-not $existing) {
    New-Service -Name $serviceName -BinaryPathName "`"$exe`"" -DisplayName 'Coho Meter Billing Engine' `
        -Description 'Imports meter reads, rates bill runs and writes print and direct debit files.' `
        -StartupType Automatic -Credential $credential | Out-Null
} else {
    & sc.exe config $serviceName obj= "$ServiceAccount" password= "$($credential.GetNetworkCredential().Password)" | Out-Null
}
& sc.exe config $serviceName start= delayed-auto | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/""/0 | Out-Null

Start-Service $serviceName
Get-Service $serviceName
