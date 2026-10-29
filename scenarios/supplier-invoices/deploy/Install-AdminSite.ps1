<#
.SYNOPSIS
    Creates the IIS site for the accounts payable web application on invoices.<zone>.
    Requires the ASP.NET Core 8 Hosting Bundle (dotnet-hosting-8.x-win.exe).
.EXAMPLE
    .\Install-AdminSite.ps1 -PublishPath C:\Build\SupplierInvoices -PoolAccount CORP\svc-inv-web -CertificateThumbprint <thumbprint>
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$PoolAccount,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [string]$ZoneName = 'corp.local',
    [string]$SitePath = 'C:\inetpub\sites\invoices',
    [string]$LogPath = 'D:\InvoiceHub\Logs\Admin',
    [string]$HostingBundle
)

$ErrorActionPreference = 'Stop'
Install-WindowsFeature Web-Server, Web-Windows-Auth, Web-Http-Redirect, Web-Mgmt-Console | Out-Null

if (-not (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) {
    if (-not $HostingBundle) { throw 'The ASP.NET Core Hosting Bundle is not installed. Pass -HostingBundle <path to dotnet-hosting-8.x-win.exe>.' }
    Start-Process $HostingBundle -ArgumentList '/install', '/quiet', '/norestart' -Wait
    net stop was /y | Out-Null
    net start w3svc | Out-Null
}
Import-Module WebAdministration

$siteName = 'invoices'
$hostName = "invoices.$ZoneName"
$credential = Get-Credential -UserName $PoolAccount -Message 'Password for the application pool account'

New-Item -ItemType Directory -Force -Path $SitePath, $LogPath | Out-Null
if (Test-Path "IIS:\AppPools\$siteName") { Stop-WebAppPool $siteName -ErrorAction SilentlyContinue }

$settings = Join-Path $SitePath 'appsettings.Production.json'
$keep = Test-Path $settings
if ($keep) { Copy-Item $settings "$settings.bak" -Force }
robocopy (Join-Path $PublishPath 'Invoices.Admin') $SitePath /MIR /NFL /NDL /NJH /NJS /XF *.pdb /XD logs | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying Invoices.Admin failed.' }
if ($LogPath -ne 'D:\InvoiceHub\Logs\Admin') {
    $webConfig = Join-Path $SitePath 'web.config'
    (Get-Content $webConfig -Raw).Replace('D:\InvoiceHub\Logs\Admin', $LogPath) | Set-Content $webConfig -Encoding UTF8
}
if ($keep) { Move-Item "$settings.bak" $settings -Force }
elseif ($ZoneName -ne 'corp.local') {
    $json = Get-Content (Join-Path $SitePath 'appsettings.json') -Raw
    $json.Replace('.corp.local', ".$ZoneName") | Set-Content $settings
}

if (-not (Test-Path "IIS:\AppPools\$siteName")) { New-WebAppPool $siteName | Out-Null }
$pool = "IIS:\AppPools\$siteName"
Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $pool -Name processModel -Value @{ identityType = 3; userName = $PoolAccount; password = $credential.GetNetworkCredential().Password; loadUserProfile = $true }

if (-not (Get-Website -Name $siteName -ErrorAction SilentlyContinue)) {
    New-Website -Name $siteName -PhysicalPath $SitePath -ApplicationPool $siteName -HostHeader $hostName -Port 80 | Out-Null
    New-WebBinding -Name $siteName -Protocol https -Port 443 -HostHeader $hostName -SslFlags 1
}
$binding = Get-WebBinding -Name $siteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint, 'My')

Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $siteName -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $siteName -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $siteName -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true

# Kerberos for the site runs as the pool account.
setspn -S "HTTP/$hostName" $PoolAccount | Out-Null

icacls $LogPath /grant "${PoolAccount}:(OI)(CI)M" | Out-Null
Start-WebAppPool $siteName
Write-Host "https://$hostName/"
