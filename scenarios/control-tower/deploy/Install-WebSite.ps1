<#
.SYNOPSIS
    Installs one Control Tower web application as an IIS site.
.DESCRIPTION
    Creates the site under C:\inetpub\sites\<app> with its own application pool (No Managed Code),
    an HTTPS binding with SNI for <app>.<zone>, the production settings file and the key and log folders.
.EXAMPLE
    .\Install-WebSite.ps1 -App tower -PublishPath C:\Build\ControlTower -ZoneName corp.local -CertificateThumbprint 3F2A... `
        -SqlPassword $towerSql -RabbitMqPassword $towerMq
.EXAMPLE
    .\Install-WebSite.ps1 -App login -PublishPath C:\Build\ControlTower -ZoneName corp.local -CertificateThumbprint 3F2A... `
        -SqlPassword $identitySql -Settings @{ 'IdentityServer:SigningCertificateThumbprint' = 'A1B2...' }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('login', 'booking', 'track', 'tower', 'scan')][string]$App,
    [Parameter(Mandatory)][string]$PublishPath,
    [string]$ZoneName = 'corp.local',
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$SqlServer,
    [Parameter(Mandatory)][object]$SqlPassword,
    [string]$RabbitMqHost,
    [object]$RabbitMqPassword,
    # Application pool identity; default is the built-in ApplicationPoolIdentity.
    [string]$PoolUser,
    [securestring]$PoolPassword,
    [hashtable]$Settings = @{},
    [string]$SitesRoot = 'C:\inetpub\sites'
)

. "$PSScriptRoot\common.ps1"
Assert-Admin
Import-Module WebAdministration

$def = $WebApps[$App]
if (-not $SqlServer) { $SqlServer = "sql01.$ZoneName" }
if (-not $RabbitMqHost) { $RabbitMqHost = "mq01.$ZoneName" }
$hostName = "$App.$ZoneName"
$sitePath = Join-Path $SitesRoot $App
$source = Join-Path $PublishPath $def.Project
$poolName = "ControlTower-$App"

if (-not (Test-Path (Join-Path $source $def.Dll))) { throw "$($def.Dll) not found in $source. Run Publish.ps1 first." }
if (-not (Test-HostingBundle -MajorVersion 6)) {
    throw 'The ASP.NET Core 6 Hosting Bundle is not installed (dotnet-hosting-6.0.x-win.exe). Install it and run "iisreset".'
}
if (-not (Get-ChildItem Cert:\LocalMachine\My | Where-Object Thumbprint -eq $CertificateThumbprint)) {
    throw "Certificate $CertificateThumbprint is not in LocalMachine\My. Import it with Import-Certificates.ps1."
}

Write-Step "Application pool $poolName"
if (-not (Test-Path "IIS:\AppPools\$poolName")) { New-WebAppPool -Name $poolName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$poolName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$poolName" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel.loadUserProfile -Value $true
Set-ItemProperty "IIS:\AppPools\$poolName" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
if ($PoolUser) {
    $pw = ConvertTo-PlainText $PoolPassword
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel -Value @{ userName = $PoolUser; password = $pw; identityType = 3 }
    $identity = $PoolUser
} else {
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    $identity = "IIS AppPool\$poolName"
}

Write-Step "Files to $sitePath"
if (Test-Path "IIS:\Sites\$App") { Stop-WebSite -Name $App -ErrorAction SilentlyContinue }
Stop-WebAppPool -Name $poolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
New-Item -ItemType Directory -Path $sitePath -Force | Out-Null
# Keep the production settings file that may have been edited on the server.
& robocopy.exe $source $sitePath /MIR /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the files failed (robocopy $LASTEXITCODE)." }

Write-Step 'Folders'
$keys = Join-Path $ControlTowerRoot "Keys\$App"
$logs = Join-Path $ControlTowerRoot 'Logs'
New-Item -ItemType Directory -Path $keys, $logs, (Join-Path $logs 'stdout') -Force | Out-Null
Grant-FolderAccess -Path $sitePath -Identity $identity -Rights ReadAndExecute
Grant-FolderAccess -Path $keys -Identity $identity -Rights Modify
Grant-FolderAccess -Path $logs -Identity $identity -Rights Modify
if ($App -in 'booking', 'scan') {
    $docs = Join-Path $ControlTowerRoot 'Documents'
    Grant-FolderAccess -Path $docs -Identity $identity -Rights Modify
}

Write-Step 'Settings'
$values = @{
    "ConnectionStrings:$($def.ConnectionName)" = (New-SqlConnectionString -Server $SqlServer -Database $def.Database -User $def.SqlLogin -Password $SqlPassword -ApplicationName "ControlTower.$App")
    'DataProtection:KeyFolder' = $keys
}
if ($def.MqUser) {
    if (-not $RabbitMqPassword) { throw "-RabbitMqPassword is required for $App." }
    $values['ConnectionStrings:RabbitMq'] = New-RabbitConnectionString -HostName $RabbitMqHost -User $def.MqUser -Password $RabbitMqPassword
}
if ($App -ne 'login') {
    $values['Authentication:Authority'] = "https://login.$ZoneName"
}
if ($App -eq 'tower') {
    $values['Spa:Authority'] = "https://login.$ZoneName"
}
foreach ($k in $Settings.Keys) { $values[$k] = $Settings[$k] }
Set-AppSettings -Path (Join-Path $sitePath 'appsettings.Production.json') -Values $values
# The settings file holds passwords: only administrators and the pool identity may read it.
$settingsFile = Join-Path $sitePath 'appsettings.Production.json'
$acl = Get-Acl $settingsFile
$acl.SetAccessRuleProtection($true, $false)
foreach ($who in 'BUILTIN\Administrators', 'NT AUTHORITY\SYSTEM', $identity) {
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($who, 'Read', 'Allow')))
}
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators', 'FullControl', 'Allow')))
Set-Acl $settingsFile $acl

Write-Step "Site $App ($hostName)"
if (-not (Test-Path "IIS:\Sites\$App")) {
    New-WebSite -Name $App -PhysicalPath $sitePath -ApplicationPool $poolName -HostHeader $hostName -Port 80 | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$App" -Name physicalPath -Value $sitePath
    Set-ItemProperty "IIS:\Sites\$App" -Name applicationPool -Value $poolName
}
Set-ItemProperty "IIS:\Sites\$App" -Name applicationDefaults.preloadEnabled -Value $true
if (-not (Get-WebBinding -Name $App -Protocol https -HostHeader $hostName)) {
    New-WebBinding -Name $App -Protocol https -Port 443 -HostHeader $hostName -SslFlags 1
}
$binding = Get-WebBinding -Name $App -Protocol https -HostHeader $hostName
$binding.AddSslCertificate($CertificateThumbprint, 'My')

# WebSockets are needed by the SignalR hubs (tower, track).
if ($App -in 'tower', 'track') {
    $ws = Get-WindowsFeature Web-WebSockets -ErrorAction SilentlyContinue
    if ($ws -and -not $ws.Installed) { Install-WindowsFeature Web-WebSockets | Out-Null }
}

Start-WebAppPool -Name $poolName
Start-WebSite -Name $App

Write-Host ''
Write-Host "https://$hostName/ installed. Logs: $logs"
