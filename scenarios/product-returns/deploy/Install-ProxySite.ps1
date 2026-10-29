<#
.SYNOPSIS
    Creates the IIS site "returns" on the front-end server (returns.<zone>). The site has no application
    code: URL Rewrite and Application Request Routing forward requests to the portal and the API.
    Install URL Rewrite 2.1 (rewrite_amd64_en-US.msi) and ARR 3.0 (requestRouter_amd64.msi) first.
.EXAMPLE
    .\Install-ProxySite.ps1 -ZoneName corp.local -CertificateThumbprint 3F2A...
#>
param(
    [string]$ZoneName = 'corp.local',
    [string]$SiteName = 'returns',
    [string]$SitePath = 'C:\inetpub\sites\returns',
    [Parameter(Mandatory)] [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Install-WindowsFeature Web-Server, Web-Http-Redirect, Web-Http-Logging, Web-Request-Monitor, Web-Filtering, Web-Mgmt-Console | Out-Null
Import-Module WebAdministration

if (-not (Test-Path 'C:\Windows\System32\inetsrv\rewrite.dll')) { throw 'URL Rewrite is not installed.' }
if (-not (Get-WebGlobalModule -Name ApplicationRequestRouting -ErrorAction SilentlyContinue)) { throw 'Application Request Routing is not installed.' }

# ARR proxy settings (server level).
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name 'enabled' -Value 'True'
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name 'preserveHostHeader' -Value 'False'
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name 'reverseRewriteHostInResponseHeaders' -Value 'False'
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name 'timeout' -Value '00:02:00'

# Server variables the rewrite rules may set.
foreach ($variable in 'HTTP_X_FORWARDED_PROTO', 'HTTP_X_FORWARDED_HOST') {
    $existing = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.webServer/rewrite/allowedServerVariables/add[@name='$variable']"
    if (-not $existing) {
        Add-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/rewrite/allowedServerVariables' -Value @{ name = $variable }
    }
}

New-Item -ItemType Directory -Force -Path $SitePath | Out-Null
$webConfig = Get-Content (Join-Path $here 'proxy\web.config') -Raw
$webConfig = $webConfig.Replace('.corp.local', ".$ZoneName")
Set-Content (Join-Path $SitePath 'web.config') $webConfig -Encoding UTF8
Copy-Item (Join-Path $here 'proxy\maintenance.htm') $SitePath -Force

if (-not (Test-Path "IIS:\AppPools\$SiteName")) {
    New-WebAppPool -Name $SiteName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''

if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -HostHeader "returns.$ZoneName" -Port 80 | Out-Null
}
if (-not (Get-WebBinding -Name $SiteName -Protocol https)) {
    New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader "returns.$ZoneName" -SslFlags 1
}
$binding = Get-WebBinding -Name $SiteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint, 'My')

Start-Website -Name $SiteName
Get-Website -Name $SiteName
