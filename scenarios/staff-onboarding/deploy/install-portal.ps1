<#
.SYNOPSIS
    Installs the Staff Onboarding portal as an IIS site on onboarding.corp.local.
.DESCRIPTION
    Run as administrator on the web server. Requires IIS with Windows Authentication and the
    ASP.NET Core 8 Hosting Bundle. Build output from
    dotnet publish src\StaffOnboarding.Portal -c Release -o C:\Install\Portal
#>
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $SiteName = 'onboarding',
    [string] $HostName = 'onboarding.corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $Domain = 'CORP',
    [string] $ServiceAccount = 'svc-onbweb',
    [int] $Port = 80
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Import-Module WebAdministration

$account = "$Domain\$ServiceAccount$"
$sitePath = Join-Path $SitesRoot $SiteName

Install-Gmsa $ServiceAccount

if (-not (Test-Path "IIS:\AppPools\$SiteName")) { New-WebAppPool -Name $SiteName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel -Value @{ identityType = 3; userName = $account; password = '' }
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.loadUserProfile -Value $true

New-Item -ItemType Directory -Force -Path $sitePath | Out-Null
Stop-WebAppPool -Name $SiteName -ErrorAction SilentlyContinue
robocopy $SourcePath $sitePath /MIR /XF 'appsettings.Production.json' /XD logs /NFL /NDL /NJH /NJS | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sitePath 'logs') | Out-Null
icacls $sitePath /grant "${account}:(OI)(CI)RX" | Out-Null
icacls (Join-Path $sitePath 'logs') /grant "${account}:(OI)(CI)M" | Out-Null

if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $sitePath -ApplicationPool $SiteName -HostHeader $HostName -Port $Port | Out-Null
}
else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $sitePath
}

# Windows authentication is unlocked so the site's web.config can configure it.
& "$env:WINDIR\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/windowsAuthentication | Out-Null
& "$env:WINDIR\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/anonymousAuthentication | Out-Null

Start-WebAppPool -Name $SiteName
Write-Host "Site $SiteName installed at $sitePath, http://$HostName/"
