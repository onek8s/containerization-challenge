<#
.SYNOPSIS
    Installs one Dealer Orders web application in IIS on web01: app pool, site with an HTTPS binding, configuration.
    portal and docs run as ApplicationPoolIdentity with SQL logins; salesdesk uses Windows authentication (Kerberos,
    the pool runs as CORP\svc-dealer-web with the HTTP/salesdesk.corp.local SPN).
.EXAMPLE
    .\Install-WebSite.ps1 -App portal -PublishPath C:\Build\DealerOrders -CertificateThumbprint 3F1A... -SqlPassword $p `
        -Settings @{ 'DocumentService.ClientKey' = '...'; 'Push.ServiceKey' = '...' }
.EXAMPLE
    .\Install-WebSite.ps1 -App salesdesk -PublishPath C:\Build\DealerOrders -CertificateThumbprint 3F1A... -SqlPassword $p `
        -PoolAccount CORP\svc-dealer-web -PoolPassword $pp
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('portal', 'salesdesk', 'docs')][string]$App,
    [Parameter(Mandatory)][string]$PublishPath,
    [string]$ZoneName = 'corp.local',
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$SqlServer,
    [Parameter(Mandatory)][object]$SqlPassword,
    [string]$PoolAccount,
    [securestring]$PoolPassword,
    [hashtable]$Settings = @{}
)

. "$PSScriptRoot\common.ps1"
Assert-Admin
Import-Module WebAdministration

$def = $WebApps[$App]
if (-not $SqlServer) { $SqlServer = "sql01.$ZoneName" }
$hostName = "$($def.HostName).$ZoneName"
$source = Join-Path $PublishPath $def.Project
$target = Join-Path $InstallRoot "Web\$($def.HostName)"

if (-not (Test-NetFramework472)) { throw '.NET Framework 4.7.2 is required.' }
foreach ($feature in 'Web-Asp-Net45', 'Web-Windows-Auth', 'NET-WCF-HTTP-Activation45', 'Web-WebSockets') {
    if (-not (Get-WindowsFeature $feature).Installed) {
        Write-Step "Windows feature $feature"
        Install-WindowsFeature $feature | Out-Null
    }
}

Write-Step "Files to $target"
if (Test-Path $target) {
    # Keep the configured Web.config: copy everything else over it.
    Copy-Item (Join-Path $target 'Web.config') "$env:TEMP\$App.Web.config" -Force
}
robocopy $source $target /MIR /XF Web.config /NFL /NDL /NJH /NJS | Out-Null
if (-not (Test-Path (Join-Path $target 'Web.config'))) { Copy-Item (Join-Path $source 'Web.config') $target }

Write-Step 'Configuration'
$values = @{ 'Logging.Folder' = (Join-Path $InstallRoot 'Logs') }
if ($App -ne 'salesdesk') { $values['Catalog.Folder'] = '' }
foreach ($k in $Settings.Keys) { $values[$k] = $Settings[$k] }
Set-ConfigValues -Path (Join-Path $target 'Web.config') -AppSettings $values `
    -ConnectionString (New-SqlConnectionString -Server $SqlServer -User $def.SqlLogin -Password $SqlPassword -ApplicationName "DealerOrders.$App")

Write-Step "App pool $($def.Pool)"
if (-not (Test-Path "IIS:\AppPools\$($def.Pool)")) { New-WebAppPool -Name $def.Pool | Out-Null }
Set-ItemProperty "IIS:\AppPools\$($def.Pool)" -Name managedRuntimeVersion -Value $def.Runtime
Set-ItemProperty "IIS:\AppPools\$($def.Pool)" -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty "IIS:\AppPools\$($def.Pool)" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$($def.Pool)" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
if ($PoolAccount) {
    Set-ItemProperty "IIS:\AppPools\$($def.Pool)" -Name processModel -Value @{ userName = $PoolAccount; password = (ConvertTo-PlainText $PoolPassword); identityType = 3 }
    $identity = $PoolAccount
} else {
    $identity = "IIS AppPool\$($def.Pool)"
}

Write-Step "Site $hostName"
$siteName = "DealerOrders-$App"
if (-not (Get-Website -Name $siteName)) {
    New-Website -Name $siteName -PhysicalPath $target -ApplicationPool $def.Pool -HostHeader $hostName -Port 80 | Out-Null
    New-WebBinding -Name $siteName -Protocol https -Port 443 -HostHeader $hostName -SslFlags 1
}
Set-ItemProperty "IIS:\Sites\$siteName" -Name applicationDefaults.preloadEnabled -Value $true
$binding = Get-WebBinding -Name $siteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint, 'My')

if ($def.WindowsAuth) {
    Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false
    Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
    # Kerberos with a domain pool account: the SPN must be on that account and kernel-mode auth must use its credentials.
    Set-WebConfigurationProperty -PSPath IIS:\ -Location $siteName -Filter system.webServer/security/authentication/windowsAuthentication -Name useAppPoolCredentials -Value $true
    Write-Host "  Check: setspn -S HTTP/$hostName $PoolAccount"
}

Write-Step 'Folder rights'
$logs = Join-Path $InstallRoot 'Logs'
New-Item $logs -ItemType Directory -Force | Out-Null
icacls $target /grant "${identity}:(OI)(CI)RX" | Out-Null
icacls $logs /grant "${identity}:(OI)(CI)M" | Out-Null

Restart-WebAppPool -Name $def.Pool
Write-Host "https://$hostName/ installed"
