<#
.SYNOPSIS
    Creates the IIS sites and app pools for the City Permits web applications.
.DESCRIPTION
    Sites under C:\inetpub\sites\<app>, one app pool per site (No Managed Code). Requires the
    ASP.NET Core 8.0 and 6.0 Hosting Bundles. The back office and map service load the
    Geomatix engine in process: run Install-GisEngine.ps1 on their server first.
.EXAMPLE
    .\Install-WebSites.ps1 -PublishRoot D:\Drops\CityPermits\3.4.0 -WebAccount CORP\svc-permits-web -WebPassword $pw -SqlPasswords @{ Citizen='..'; OpenData='..'; Inspections='..' }
    .\Install-WebSites.ps1 -PublishRoot D:\Drops\CityPermits\3.4.0 -Lab
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $WebAccount = 'CORP\svc-permits-web',
    [securestring] $WebPassword,
    [hashtable] $SqlPasswords = @{},
    [string] $ZoneName = 'corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $CertificateThumbprint,
    [string] $MapKey = '',
    [switch] $Lab
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator
$cfg = if ($Lab) { Import-PowerShellDataFile (Join-Path $PSScriptRoot 'lab.psd1') } else { @{ GisProvider = 'Geomatix'; ArchiveProvider = 'FileTable'; EidMode = 'Saml2'; BackOfficeAuth = 'WsFederation'; InspectionsAuth = 'Adfs'; PaymentsMode = 'Live'; SqlServer = 'permits-sql' } }
$sql = "$($cfg.SqlServer).$ZoneName"

if (-not $Lab -and -not (Test-GeomatixEngine)) { throw 'Geomatix engine is not ready on this server (Install-GisEngine.ps1).' }

$sites = @(
    @{ Name = 'permits'; Folder = 'citizen'; Settings = @{
        Database = @{ Provider = 'SqlServer' }
        ConnectionStrings = @{ Permits = "Server=$sql;Database=CityPermits;User Id=permits_citizen;Password=$($SqlPasswords.Citizen);Encrypt=True" }
        Archive = @{ Provider = $cfg.ArchiveProvider; FolderPath = $cfg.ArchiveFolder }
        Eid = @{ Mode = $cfg.EidMode }; Paystream = @{ Mode = $cfg.PaymentsMode }
        MapService = @{ BaseUrl = "https://maps.$ZoneName/"; Key = $MapKey } } },
    @{ Name = 'permits-office'; Folder = 'backoffice'; Identity = $true; Settings = @{
        Archive = @{ Provider = $cfg.ArchiveProvider; FolderPath = $cfg.ArchiveFolder }
        Authentication = @{ Mode = $cfg.BackOfficeAuth }
        Gis = @{ Provider = $cfg.GisProvider; DevelopmentDataPath = $cfg.GisDataPath }
        MapService = @{ TileUrl = "https://maps.$ZoneName/tiles/permits/{z}/{x}/{y}.png" } } },
    @{ Name = 'maps'; Folder = 'maps'; Identity = $true; Settings = @{ Gis = @{ Provider = $cfg.GisProvider; DevelopmentDataPath = $cfg.GisDataPath } } },
    @{ Name = 'inspect'; Folder = 'inspections'; Settings = @{
        ConnectionStrings = @{ Permits = "Server=$sql;Database=CityPermits;User Id=permits_inspections;Password=$($SqlPasswords.Inspections);Encrypt=True" }
        Authentication = @{ Mode = $cfg.InspectionsAuth } } },
    @{ Name = 'opendata'; Folder = 'opendata'; Settings = @{
        ConnectionStrings = @{ Permits = "Server=$sql;Database=CityPermits;User Id=permits_opendata;Password=$($SqlPasswords.OpenData);Encrypt=True;ApplicationIntent=ReadOnly" } } }
)

foreach ($s in $sites) {
    $path = Join-Path $SitesRoot $s.Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    Copy-Item (Join-Path $PublishRoot "$($s.Folder)\*") $path -Recurse -Force
    Write-JsonSettings -Path (Join-Path $path 'appsettings.Production.json') -Settings $s.Settings

    if (-not (Test-Path "IIS:\AppPools\$($s.Name)")) { New-WebAppPool -Name $s.Name | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name startMode -Value AlwaysRunning
    Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    if ($s.Identity -and $WebPassword) {
        # The engine needs a loaded user profile for its licence cache.
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($WebPassword))
        Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name processModel -Value @{ userName = $WebAccount; password = $plain; identityType = 3; loadUserProfile = $true }
        Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name processModel.maxProcesses -Value 1
    }
    $hostName = "$($s.Name).$ZoneName"
    if (-not (Get-Website -Name $s.Name)) {
        New-Website -Name $s.Name -PhysicalPath $path -ApplicationPool $s.Name -HostHeader $hostName -Port 443 -Ssl | Out-Null
    }
    if ($CertificateThumbprint) {
        $binding = Get-WebBinding -Name $s.Name -Protocol https
        $binding.AddSslCertificate($CertificateThumbprint, 'My')
    }
    Write-Host "Site $hostName -> $path"
}
