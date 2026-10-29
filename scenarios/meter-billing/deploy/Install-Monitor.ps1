<#
.SYNOPSIS
    Installs Billing Monitor on a billing operator workstation and adds a Start menu shortcut.
.EXAMPLE
    .\Install-Monitor.ps1 -PublishPath \\files01.corp.local\Software\MeterBilling
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [string]$InstallPath = 'C:\Program Files\Coho\Billing Monitor',
    [string]$EngineUrl = 'http://billing-engine.corp.local:8441/',
    [string]$EngineLogShare = '\\billing-engine.corp.local\BillingLogs$',
    [string]$SqlServer = 'sql01.corp.local'
)

$ErrorActionPreference = 'Stop'

$release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
if (-not $release -or $release -lt 528040) { throw '.NET Framework 4.8 is required' }

Get-Process BillingMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Copy-Item (Join-Path $PublishPath 'Monitor\*') $InstallPath -Recurse -Force

$configFile = Join-Path $InstallPath 'BillingMonitor.exe.config'
[xml]$config = Get-Content $configFile
($config.configuration.appSettings.add | Where-Object { $_.key -eq 'EngineUrl' }).value = $EngineUrl
($config.configuration.appSettings.add | Where-Object { $_.key -eq 'EngineLogShare' }).value = $EngineLogShare
($config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'MeterBilling' }).connectionString =
    "Data Source=$SqlServer;Initial Catalog=MeterBilling;Integrated Security=SSPI;Application Name=Billing Monitor"
$config.Save($configFile)

$shortcutPath = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Coho\Billing Monitor.lnk'
New-Item -ItemType Directory -Path (Split-Path $shortcutPath) -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $InstallPath 'BillingMonitor.exe'
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Save()

Write-Host "Billing Monitor installed to $InstallPath"
