<#
.SYNOPSIS
    Creates the IIS site for the Order Hub portal on hub.<zone> and writes its production settings.
.EXAMPLE
    .\Install-Portal.ps1 -PublishPath C:\Build\OrderHub -HostName hub.corp.local -SqlServer sql01.corp.local `
        -SqlPassword (Read-Host -AsSecureString) -RabbitMqPassword (Read-Host -AsSecureString) `
        -AdminUser hubadmin -AdminPassword (Read-Host -AsSecureString) -HostingBundle C:\Install\dotnet-hosting-8.0.x-win.exe
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $SiteName = 'orderhub',
    [string] $SitePath = 'C:\inetpub\sites\orderhub',
    [string] $HostName = 'hub.corp.local',
    [string] $CertificateThumbprint,
    [string] $SqlServer = 'sql01.corp.local',
    [string] $DatabaseName = 'OrderHub',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [string] $RabbitMqHost = 'rabbitmq://mq.corp.local/orderhub',
    [Parameter(Mandatory)] [securestring] $RabbitMqPassword,
    [string] $DataRoot = 'C:\OrderHub',
    [string] $AdminUser,
    [securestring] $AdminPassword,
    [string] $HostingBundle
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Administrator

foreach ($feature in 'Web-Server', 'Web-Static-Content', 'Web-Http-Errors', 'Web-Http-Logging', 'Web-Filtering', 'Web-Mgmt-Console') {
    if (-not (Get-WindowsFeature $feature).Installed) { Install-WindowsFeature $feature | Out-Null }
}
if ($HostingBundle) {
    # Provides the ASP.NET Core Module V2 that starts the portal executable.
    Start-Process $HostingBundle -ArgumentList '/install', '/quiet', '/norestart' -Wait
    & iisreset.exe /restart | Out-Null
}
Import-Module WebAdministration

$source = Join-Path $PublishPath 'Portal'
if (-not (Test-Path (Join-Path $source 'OrderHub.Portal.exe'))) { throw "Portal not found in $source. Run Publish.ps1 first." }

if (-not (Test-Path "IIS:\AppPools\$SiteName")) {
    New-WebAppPool -Name $SiteName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.loadUserProfile -Value $true
$poolAccount = "IIS AppPool\$SiteName"

if (Test-Path "IIS:\Sites\$SiteName") { Stop-Website $SiteName }
New-Item -ItemType Directory -Force -Path $SitePath | Out-Null
Copy-Item (Join-Path $source '*') $SitePath -Recurse -Force

$production = @{
    ConnectionStrings = @{ OrderHub = (New-SqlConnectionString $SqlServer $DatabaseName 'orderhub_portal' (ConvertTo-PlainText $SqlPassword) 'OrderHub.Portal') }
    RabbitMq          = @{ Host = $RabbitMqHost; Username = 'orderhub-portal'; Password = (ConvertTo-PlainText $RabbitMqPassword) }
    DataProtection    = @{ KeyFolder = (Join-Path $DataRoot 'Portal\Keys') }
    Logging           = @{ Folder = (Join-Path $DataRoot 'Logs') }
}
$production | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $SitePath 'appsettings.Production.json') -Encoding UTF8

Grant-FolderAccess -Path $SitePath -Account $poolAccount -Rights 'ReadAndExecute'
Grant-FolderAccess -Path (Join-Path $DataRoot 'Logs') -Account $poolAccount
Grant-FolderAccess -Path (Join-Path $DataRoot 'Portal\Keys') -Account $poolAccount

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -HostHeader $HostName -Port 80 | Out-Null
}
else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
}

if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName)) {
        New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    }
    $binding = Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName
    $binding.AddSslCertificate($CertificateThumbprint, 'My')
}

Start-Website $SiteName

if ($AdminUser) {
    Push-Location $SitePath
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Production'
        & .\OrderHub.Portal.exe create-user $AdminUser (ConvertTo-PlainText $AdminPassword) Admin
        if ($LASTEXITCODE -ne 0) { throw 'Creating the portal administrator failed.' }
    }
    finally { Pop-Location }
}
Write-Host "Portal installed: http://$HostName/"
