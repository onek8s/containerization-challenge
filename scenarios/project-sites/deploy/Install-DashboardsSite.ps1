<#
.SYNOPSIS
    Installs the provider-hosted dashboards add-in web site on IIS (dashboards.corp.local): .NET Framework 4.7.2,
    integrated pipeline, Windows authentication, HTTPS binding, signing certificate for the S2S tokens.
.EXAMPLE
    .\Install-DashboardsSite.ps1 -Version 5.3.1 -CertificateThumbprint 0F3A... -PfxPath \\fs01\Install$\ProjectSites\Certificates\ProjectSitesS2S.pfx
#>
param(
    [string] $Version,
    # TLS certificate for dashboards.corp.local in LocalMachine\My.
    [Parameter(Mandatory = $true)] [string] $CertificateThumbprint,
    # S2S signing certificate with private key; imported into LocalMachine\My.
    [Parameter(Mandatory = $true)] [string] $PfxPath,
    [securestring] $PfxPassword = (Read-Host -AsSecureString 'PFX password'),
    [string] $AppPoolAccount = 'CORP\svc-ps-dashboards',
    [securestring] $AppPoolPassword = (Read-Host -AsSecureString 'App pool account password')
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Assert-Elevated
$settings = Get-ProjectSitesSettings
$drop = Get-ProjectSitesDrop -Settings $settings -Version $Version

Write-Step 'Windows features'
Install-WindowsFeature Web-Server, Web-Asp-Net45, Web-Windows-Auth, Web-Http-Redirect, Web-Mgmt-Console, NET-Framework-45-ASPNET | Out-Null
$release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
if ($release -lt 461808) { throw ".NET Framework 4.7.2 or later is required (release key $release)." }

Write-Step 'Signing certificate'
$signing = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $PfxPassword -Exportable:$false
$keyFile = $signing.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
$keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$keyFile"
& icacls.exe $keyPath /grant "${AppPoolAccount}:R" | Out-Null

Write-Step 'Files'
New-Item -ItemType Directory -Path $settings.DashboardsPath, $settings.DashboardsLogs -Force | Out-Null
if (Test-Path (Join-Path $settings.DashboardsPath 'Web.config')) {
    Copy-Item (Join-Path $settings.DashboardsPath 'Web.config') (Join-Path $env:TEMP 'ProjectDashboards.Web.config.bak') -Force
    Set-Content (Join-Path $settings.DashboardsPath 'app_offline.htm') '<p>Project dashboards are being updated.</p>'
}
& robocopy.exe (Join-Path $drop 'addin\site') $settings.DashboardsPath /MIR /XF Web.config app_offline.htm /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)." }
if (-not (Test-Path (Join-Path $settings.DashboardsPath 'Web.config'))) {
    Copy-Item (Join-Path $drop 'addin\site\Web.config') $settings.DashboardsPath
}
& icacls.exe $settings.DashboardsLogs /grant "${AppPoolAccount}:(OI)(CI)M" | Out-Null

Write-Step 'Web.config settings'
$configPath = Join-Path $settings.DashboardsPath 'Web.config'
[xml] $config = Get-Content $configPath
$appSettings = $config.configuration.appSettings
($appSettings.add | Where-Object key -eq 'ClientSigningCertificateThumbprint').value = $signing.Thumbprint
($appSettings.add | Where-Object key -eq 'HubUrl').value = $settings.HubUrl
$config.Save($configPath)

Write-Step 'IIS site'
Set-ProjectSitesAppPool -Name $settings.DashboardsAppPool -UserName $AppPoolAccount -Password $AppPoolPassword
Import-Module WebAdministration
if (-not (Get-Website -Name 'ProjectDashboards')) {
    New-Website -Name 'ProjectDashboards' -PhysicalPath $settings.DashboardsPath -ApplicationPool $settings.DashboardsAppPool `
        -HostHeader $settings.DashboardsHost -Port 443 -Ssl | Out-Null
    (Get-WebBinding -Name 'ProjectDashboards' -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
}
Set-WebConfigurationProperty -PSPath IIS:\ -Location 'ProjectDashboards' -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath IIS:\ -Location 'ProjectDashboards' -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
# Kerberos: the pool account needs the HTTP/dashboards.corp.local SPN (setspn -S HTTP/dashboards.corp.local CORP\svc-ps-dashboards).
Set-WebConfigurationProperty -PSPath IIS:\ -Location 'ProjectDashboards' -Filter system.webServer/security/authentication/windowsAuthentication -Name useAppPoolCredentials -Value $true

Remove-Item (Join-Path $settings.DashboardsPath 'app_offline.htm') -ErrorAction SilentlyContinue
Write-Step "Installed https://$($settings.DashboardsHost)/"
