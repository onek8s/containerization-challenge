<#
.SYNOPSIS
    Installs the bill lookup site (IIS site 'billing', https://billing.<zone>/) on the web server.
.EXAMPLE
    .\Install-LookupSite.ps1 -PublishPath C:\Build\MeterBilling -PoolAccount CORP\svc-billing-web -CertificateThumbprint 3F2A... -HostingBundle .\dotnet-hosting-8.0.x-win.exe
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$PoolAccount,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [string]$HostName = 'billing.corp.local',
    [string]$SitePath = 'C:\inetpub\sites\billing',
    [string]$SqlServer = 'sql01.corp.local',
    [string]$EngineUrl = 'http://billing-engine.corp.local:8441/',
    [string[]]$AllowedGroups = @('CORP\Billing-CustomerService', 'CORP\Billing-Operators'),
    [string]$HostingBundle,
    [securestring]$Password
)

$ErrorActionPreference = 'Stop'
$siteName = 'billing'

Install-WindowsFeature Web-Server, Web-Windows-Auth, Web-Http-Redirect, Web-Mgmt-Console | Out-Null
if ($HostingBundle) {
    Start-Process $HostingBundle -ArgumentList '/install', '/quiet', '/norestart' -Wait
    & net stop was /y | Out-Null
    & net start w3svc | Out-Null
}

Import-Module WebAdministration
if (-not $Password) { $Password = Read-Host "Password for $PoolAccount" -AsSecureString }
$plain = (New-Object System.Management.Automation.PSCredential($PoolAccount, $Password)).GetNetworkCredential().Password

# Files
if (Test-Path "IIS:\Sites\$siteName") { Stop-WebSite $siteName }
New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
Copy-Item (Join-Path $PublishPath 'Lookup\*') $SitePath -Recurse -Force
& icacls $SitePath /grant "${PoolAccount}:(OI)(CI)RX" | Out-Null

$settings = @{
    ConnectionStrings = @{ MeterBilling = "Server=$SqlServer;Database=MeterBilling;Integrated Security=true;TrustServerCertificate=true;Application Name=MeterBilling.Lookup" }
    Lookup            = @{ EngineUrl = $EngineUrl; AllowedGroups = $AllowedGroups }
}
$settings | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $SitePath 'appsettings.Production.json') -Encoding UTF8

# Application pool
if (-not (Test-Path "IIS:\AppPools\$siteName")) { New-WebAppPool $siteName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$siteName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel -Value @{ userName = $PoolAccount; password = $plain; identityType = 3 }
Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel.loadUserProfile -Value $true

# Site and binding
if (-not (Test-Path "IIS:\Sites\$siteName")) {
    New-WebSite -Name $siteName -PhysicalPath $SitePath -ApplicationPool $siteName -HostHeader $HostName -Port 443 -Ssl -SslFlags 1 | Out-Null
}
$binding = Get-WebBinding -Name $siteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint, 'My')
if (-not (Get-WebBinding -Name $siteName -Protocol http)) {
    New-WebBinding -Name $siteName -Protocol http -Port 80 -HostHeader $HostName
}

# Windows authentication (kernel mode, app pool credentials for the HTTP/<host> SPN on the pool account)
& "$env:windir\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/windowsAuthentication | Out-Null
& "$env:windir\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/anonymousAuthentication | Out-Null
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name useAppPoolCredentials -Value $true
Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false

Start-WebSite $siteName
Write-Host "https://$HostName/ installed. Register the SPN once: setspn -S HTTP/$HostName $PoolAccount"
