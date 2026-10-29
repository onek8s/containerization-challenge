<#
.SYNOPSIS
    Installs the Credit Desk intranet site on IIS (creditdesk.fabrikam.local).

.PARAMETER SourcePath
    Folder containing the built site (src\CreditDesk.Web after a Release build: Web.config,
    Global.asax, bin, Views, Content).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $HostName = 'creditdesk.fabrikam.local',
    [string] $CreditServiceAddress = 'net.tcp://credit01.fabrikam.local:8524/CreditDesk/CreditService',
    [string] $CreditServiceSpn = 'CreditDesk/credit01.fabrikam.local',
    [string] $SiteRoot = 'C:\inetpub\CreditDesk',
    [string] $AppPoolAccount = 'FABRIKAM\svc-creditdesk-web',
    [Parameter(Mandatory)] [securestring] $AppPoolPassword,
    [string] $CertificateThumbprint
)

$ErrorActionPreference = 'Stop'

Write-Host 'Installing IIS features'
Install-WindowsFeature Web-Server, Web-Windows-Auth, Web-Asp-Net45, Web-Net-Ext45, Web-ISAPI-Ext, Web-ISAPI-Filter,
    Web-Http-Redirect, Web-Mgmt-Console, Web-Scripting-Tools, NET-WCF-TCP-Activation45 | Out-Null
Import-Module WebAdministration

Write-Host "Copying site to $SiteRoot"
New-Item $SiteRoot -ItemType Directory -Force | Out-Null
foreach ($item in 'bin', 'Views', 'Content', 'Global.asax', 'Web.config') {
    Copy-Item (Join-Path $SourcePath $item) $SiteRoot -Recurse -Force
}
Get-ChildItem (Join-Path $SiteRoot 'bin') -Include *.pdb -Recurse | Remove-Item

Write-Host 'Writing client endpoint'
$configPath = Join-Path $SiteRoot 'Web.config'
[xml] $config = Get-Content $configPath
$endpoint = $config.configuration.'system.serviceModel'.client.endpoint
$endpoint.address = $CreditServiceAddress
$endpoint.identity.servicePrincipalName.value = $CreditServiceSpn
$config.Save($configPath)

$poolName = 'CreditDesk'
if (-not (Test-Path "IIS:\AppPools\$poolName")) { New-WebAppPool $poolName | Out-Null }
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($AppPoolPassword))
Set-ItemProperty "IIS:\AppPools\$poolName" -Name managedRuntimeVersion -Value 'v4.0'
Set-ItemProperty "IIS:\AppPools\$poolName" -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty "IIS:\AppPools\$poolName" -Name processModel -Value @{ userName = $AppPoolAccount; password = $plain; identityType = 3; loadUserProfile = $true }
Set-ItemProperty "IIS:\AppPools\$poolName" -Name recycling.periodicRestart.time -Value '00:00:00'
Set-ItemProperty "IIS:\AppPools\$poolName" -Name recycling.periodicRestart.schedule -Value @{ value = '03:00:00' }

& icacls.exe $SiteRoot /grant "${AppPoolAccount}:(OI)(CI)RX" /grant 'IIS_IUSRS:(OI)(CI)RX' /T /Q | Out-Null

$siteName = 'Credit Desk'
if (-not (Get-Website -Name $siteName)) {
    New-Website -Name $siteName -PhysicalPath $SiteRoot -ApplicationPool $poolName -HostHeader $HostName -Port 80 | Out-Null
}
if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $siteName -Protocol https)) {
        New-WebBinding -Name $siteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    }
    $binding = Get-WebBinding -Name $siteName -Protocol https
    $binding.AddSslCertificate($CertificateThumbprint, 'My')
}

# Authentication settings are locked at server level by default; unlock and set for the site.
& "$env:windir\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/windowsAuthentication | Out-Null
& "$env:windir\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/anonymousAuthentication | Out-Null
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name useAppPoolCredentials -Value $true

Restart-WebAppPool $poolName
Write-Host "Credit Desk site installed: http://$HostName/"
Write-Host "Add $HostName to the Local intranet zone (GPO) so browsers sign in automatically."
