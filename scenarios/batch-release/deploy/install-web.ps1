<#
.SYNOPSIS
    Publishes the portal (release.corp.local) and the MES/ERP API (release-api.corp.local) to IIS.
.DESCRIPTION
    Prerequisites: IIS with Windows Authentication, the ASP.NET Core 8 Hosting Bundle, the .NET 8 SDK.
    Each application gets its own site and application pool under C:\inetpub\sites\<app>. The pool
    accounts must be listed as trusted callers on the signing host (install-signing-host.ps1).
    With a domain pool account register the SPN so browsers use Kerberos:
    setspn -S HTTP/release.corp.local CORP\svc-release-web
.EXAMPLE
    .\install-web.ps1 -CertificateThumbprint 3F2A... -PortalPoolAccount 'CORP\svc-release-web' -PortalPoolPassword $pw `
        -ApiPoolAccount 'CORP\svc-release-api' -ApiPoolPassword $pw2
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $PortalHost = 'release.corp.local',
    [string] $ApiHost = 'release-api.corp.local',
    [string] $SqlInstance = 'release-sql.corp.local',
    [string] $DatabaseName = 'BatchRelease',
    [string] $SigningHostUrl = 'https://release-sign.corp.local:5443/',
    [string] $CertificateThumbprint,
    [Parameter(Mandatory)] [string] $PortalPoolAccount,
    [Parameter(Mandatory)] [securestring] $PortalPoolPassword,
    [Parameter(Mandatory)] [string] $ApiPoolAccount,
    [Parameter(Mandatory)] [securestring] $ApiPoolPassword
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if (-not (Get-WebGlobalModule -Name 'AspNetCoreModuleV2' -ErrorAction SilentlyContinue)) {
    throw 'AspNetCoreModuleV2 is not registered. Install the ASP.NET Core 8 Hosting Bundle.'
}

$src = Join-Path $PSScriptRoot '..\src'
$connection = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"

function Publish-App([string] $Project, [string] $Target) {
    if (Test-Path (Join-Path $Target 'web.config')) {
        Set-Content -Path (Join-Path $Target 'app_offline.htm') -Value '<p>Batch Release is being updated.</p>'
        Start-Sleep -Seconds 3
    }
    & dotnet publish (Join-Path $src $Project) -c Release -o $Target
    if ($LASTEXITCODE -ne 0) { throw "Publishing $Project failed." }
    Remove-Item (Join-Path $Target 'app_offline.htm') -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $Target 'logs') -Force | Out-Null
}

function Set-Pool([string] $Name, [string] $Account, [securestring] $Password) {
    if (-not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null }
    $pool = "IIS:\AppPools\$Name"
    Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $pool -Name processModel.loadUserProfile -Value $true
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
    Set-ItemProperty $pool -Name processModel -Value @{ userName = $Account; password = $plain; identityType = 3 }
}

function Set-Site([string] $Name, [string] $HostName, [string] $Path) {
    if (-not (Get-Website -Name $Name)) {
        New-Website -Name $Name -PhysicalPath $Path -ApplicationPool $Name -HostHeader $HostName -Port 80 | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $Path
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $Name
    }
    if ($CertificateThumbprint) {
        if (-not (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName)) {
            New-WebBinding -Name $Name -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
        }
        (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName).AddSslCertificate($CertificateThumbprint, 'My')
    }
}

$signing = @{ BaseAddress = $SigningHostUrl }

# Portal
$portalPath = Join-Path $SiteRoot 'release'
Publish-App 'BatchRelease.Portal\BatchRelease.Portal.csproj' $portalPath
@{
    ConnectionStrings = @{ BatchRelease = "$connection;Application Name=Batch Release Portal" }
    SigningHost = $signing
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $portalPath 'appsettings.Production.json') -Encoding UTF8
Set-Pool 'release' $PortalPoolAccount $PortalPoolPassword
Set-Site 'release' $PortalHost $portalPath
Set-WebConfiguration -Filter '/system.webServer/security/authentication/windowsAuthentication' -PSPath 'IIS:\' -Location 'release' -Metadata overrideMode -Value Allow
Set-WebConfiguration -Filter '/system.webServer/security/authentication/anonymousAuthentication' -PSPath 'IIS:\' -Location 'release' -Metadata overrideMode -Value Allow
Set-WebConfigurationProperty -Filter '/system.webServer/security/authentication/windowsAuthentication' -PSPath 'IIS:\' -Location 'release' -Name useAppPoolCredentials -Value $true

# API
$apiPath = Join-Path $SiteRoot 'release-api'
Publish-App 'BatchRelease.Api\BatchRelease.Api.csproj' $apiPath
@{
    ConnectionStrings = @{ BatchRelease = "$connection;Application Name=Batch Release API" }
    SigningHost = $signing
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $apiPath 'appsettings.Production.json') -Encoding UTF8
Set-Pool 'release-api' $ApiPoolAccount $ApiPoolPassword
Set-Site 'release-api' $ApiHost $apiPath

Restart-WebAppPool -Name 'release'
Restart-WebAppPool -Name 'release-api'
$scheme = if ($CertificateThumbprint) { 'https' } else { 'http' }
Write-Host "Portal: ${scheme}://$PortalHost/  API: ${scheme}://$ApiHost/health"
