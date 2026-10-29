<#
.SYNOPSIS
    Installs the Document Control portal as IIS site 'doccontrol' with host name doccontrol.<zone>.
    Run as administrator on the web server. Requires IIS with Windows Authentication and the
    ASP.NET Core 8 Hosting Bundle.
.EXAMPLE
    .\Install-PortalSite.ps1 -PublishPath C:\Build\DocControl\Web -AppPoolAccount CORP\svc-dc-web
.EXAMPLE
    .\Install-PortalSite.ps1 -PublishPath C:\Build\DocControl\Web -AppPoolAccount CORP\svc-dc-web -ZoneName lab.local -CertificateThumbprint 1A2B...
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$AppPoolAccount,
    [string]$ZoneName = 'corp.local',
    [string]$SiteName = 'doccontrol',
    [string]$SitesRoot = 'C:\inetpub\sites',
    [string]$SqlServer = "sql01.$ZoneName",
    [string]$FileServer = "files01.$ZoneName",
    [string]$Domain = 'CORP',
    [string]$CertificateThumbprint,
    [string]$DataPath = 'D:\DocControl\Web'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$hostName = "$SiteName.$ZoneName"
$sitePath = Join-Path $SitesRoot $SiteName

if (-not (Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2')) {
    throw 'The ASP.NET Core Hosting Bundle (8.0) is not installed.'
}
foreach ($feature in 'Web-Windows-Auth', 'Web-Filtering') {
    if (-not (Get-WindowsFeature $feature).Installed) { Install-WindowsFeature $feature | Out-Null }
}

# Files. appsettings.Production.json is machine specific and not overwritten.
New-Item -ItemType Directory -Force -Path $sitePath, $DataPath, (Join-Path $sitePath 'logs') | Out-Null
if (Test-Path "IIS:\AppPools\$SiteName") { Stop-WebAppPool -Name $SiteName -ErrorAction SilentlyContinue; Start-Sleep 3 }
robocopy $PublishPath $sitePath /MIR /NFL /NDL /NJH /NJS /XF appsettings.Production.json *.pdb /XD logs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the portal failed ($LASTEXITCODE)." }

$settings = [ordered]@{
    DocControl = [ordered]@{
        Database = @{ Provider = 'SqlServer'; ConnectionString = "Server=$SqlServer;Database=DocControl;Integrated Security=True;TrustServerCertificate=True;Application Name=DocControl.Web" }
        Storage  = @{ VaultRoot = "\\$FileServer\DocVault$"; TransmittalRoot = "\\$FileServer\Transmittals$"; DropRoot = "\\$FileServer\DocDrop$" }
        Portal   = @{ ControllersGroup = "$Domain\DocControl-Controllers"; EngineersGroup = "$Domain\DocControl-Engineers" }
    }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $sitePath 'appsettings.Production.json') -Encoding UTF8

# Application pool: no managed code, domain account (needs the file shares and SQL Server).
$credential = Get-Credential -UserName $AppPoolAccount -Message "Password for the $SiteName application pool"
if (-not (Test-Path "IIS:\AppPools\$SiteName")) { New-WebAppPool -Name $SiteName | Out-Null }
$pool = "IIS:\AppPools\$SiteName"
Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
# The user profile holds the ASP.NET Core data protection key ring (anti-forgery tokens, cookies).
Set-ItemProperty $pool -Name processModel -Value @{ userName = $credential.UserName; password = $credential.GetNetworkCredential().Password; identityType = 3; loadUserProfile = $true }

# Site
if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $sitePath -ApplicationPool $SiteName -HostHeader $hostName -Port 80 | Out-Null
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $sitePath
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $SiteName

if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $SiteName -Protocol https)) {
        New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $hostName -SslFlags 1
    }
    (Get-WebBinding -Name $SiteName -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
}

# Windows authentication only; kernel mode with the app pool credentials so the Kerberos SPN is on the service account.
Set-WebConfigurationProperty -PSPath IIS:\ -Location $SiteName -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath IIS:\ -Location $SiteName -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath IIS:\ -Location $SiteName -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true
Write-Host "Register the SPN if not done yet: setspn -S HTTP/$hostName $AppPoolAccount"

icacls $DataPath /grant "${AppPoolAccount}:(OI)(CI)M" | Out-Null
icacls (Join-Path $sitePath 'logs') /grant "${AppPoolAccount}:(OI)(CI)M" | Out-Null
& (Join-Path $PSScriptRoot 'Set-InstallLocation.ps1') -Component Web -InstallPath $sitePath -DataPath $DataPath

Start-WebAppPool -Name $SiteName
Write-Host "Portal installed: http://$hostName/"
