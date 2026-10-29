<#
.SYNOPSIS
    Creates the IIS sites of Study Data: site portal (edc), monitoring (monitor) and sponsor API (sponsor-api).
.DESCRIPTION
    Prerequisites: IIS 10, ASP.NET Core 8 Hosting Bundle, a TLS certificate for the host names in
    LocalMachine\My. On the server that hosts edc: install-office-automation.ps1. On the server that hosts
    monitor: install-pdf-printer.ps1 and install-coding-engine.ps1.
    Publish first (from the scenario folder):
      dotnet publish src\StudyData.Edc.Web -c Release -o publish\edc
      dotnet publish src\StudyData.Monitor.Api -c Release -o publish\monitor
      dotnet publish src\StudyData.Sponsor.Api -c Release -o publish\sponsor-api
.EXAMPLE
    .\install-web.ps1 -PublishRoot ..\publish -AppPoolAccount 'CORP\svc-studydata-web' -CertificateThumbprint 3F2A...
.EXAMPLE
    .\install-web.ps1 -PublishRoot ..\publish -AppPoolAccount 'HOST01\svc-studydata-web' -Sites edc,sponsor-api -Http
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [Parameter(Mandatory)] [string] $AppPoolAccount,
    [string[]] $Sites = @('edc', 'monitor', 'sponsor-api'),
    [string] $DomainName = 'corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $SqlServer = 'sql01',
    [string] $DataRoot = '\\corp.local\dfs\StudyData',
    [string] $AdfsHost = 'adfs',
    [string] $MonitorClientId = 'a7d52f0e-3b8c-4e5a-9a1f-6c2b84d1e903',
    [string] $CertificateThumbprint,
    [switch] $Http,
    [string] $Environment = 'Production'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$credential = Get-Credential -UserName $AppPoolAccount -Message 'Application pool account'
$connection = "Server=$SqlServer.$DomainName;Database=StudyData;Integrated Security=true;TrustServerCertificate=true"
$monitorSecret = $null
if ($Sites -contains 'monitor') {
    $monitorSecret = Read-Host -Prompt 'ADFS client secret of the monitor application' -AsSecureString
}

$definitions = @{
    'edc'         = @{ Source = 'edc'; Exe = 'StudyData.Edc.Web' }
    'monitor'     = @{ Source = 'monitor'; Exe = 'StudyData.Monitor.Api' }
    'sponsor-api' = @{ Source = 'sponsor-api'; Exe = 'StudyData.Sponsor.Api' }
}

foreach ($name in $Sites) {
    $d = $definitions[$name]
    $hostName = "$name.$DomainName"
    $path = Join-Path $SitesRoot $name
    New-Item -ItemType Directory -Force -Path $path | Out-Null

    if (-not (Test-Path "IIS:\AppPools\$name")) {
        New-WebAppPool -Name $name | Out-Null
    }
    $pool = "IIS:\AppPools\$name"
    Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $pool -Name processModel -Value @{
        identityType = 3
        userName = $credential.UserName
        password = $credential.GetNetworkCredential().Password
        loadUserProfile = $true
        idleTimeout = [TimeSpan]::Zero
    }
    # One worker process: the calculation engines, the render thread and the notification relay live in it.
    Set-ItemProperty $pool -Name processModel.maxProcesses -Value 1
    Set-ItemProperty $pool -Name startMode -Value AlwaysRunning
    Set-ItemProperty $pool -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
    Set-ItemProperty $pool -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' }
    Stop-WebAppPool -Name $name -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3

    Copy-Item (Join-Path $PublishRoot "$($d.Source)\*") $path -Recurse -Force
    icacls $path /grant "$($AppPoolAccount):(OI)(CI)RX" | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $path 'logs') | Out-Null
    icacls (Join-Path $path 'logs') /grant "$($AppPoolAccount):(OI)(CI)M" | Out-Null

    if (-not (Get-Website -Name $name)) {
        if ($Http) {
            New-Website -Name $name -PhysicalPath $path -ApplicationPool $name -HostHeader $hostName -Port 80 | Out-Null
        }
        else {
            New-Website -Name $name -PhysicalPath $path -ApplicationPool $name -HostHeader $hostName -Port 443 -Ssl -SslFlags 1 | Out-Null
            (Get-WebBinding -Name $name -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
        }
    }
    Set-ItemProperty "IIS:\Sites\$name" -Name applicationDefaults.preloadEnabled -Value $true
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $name `
        -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $true
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $name `
        -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $false

    $vars = [ordered]@{
        'ASPNETCORE_ENVIRONMENT'       = $Environment
        'ConnectionStrings__StudyData' = $connection
        'Logging__LogFile'             = (Join-Path $path 'logs\app-.log')
    }
    switch ($name) {
        'edc' {
            $vars['Edc__SourceDocumentVault'] = "$DataRoot\SourceDocuments"
            $vars['Calculation__WorkbookRoot'] = "$DataRoot\Studies"
        }
        'monitor' {
            $vars['Monitor__Authority'] = "https://$AdfsHost.$DomainName/adfs"
            $vars['Monitor__ClientId'] = $MonitorClientId
            $vars['Monitor__ClientSecret'] = [System.Net.NetworkCredential]::new('', $monitorSecret).Password
        }
        'sponsor-api' {
            $vars['Sponsor__Authority'] = "https://$AdfsHost.$DomainName/adfs"
            $vars['Sponsor__Audience'] = "https://$hostName/"
        }
    }
    $filter = 'system.webServer/aspNetCore/environmentVariables'
    foreach ($key in $vars.Keys) {
        Remove-WebConfigurationProperty -PSPath "IIS:\Sites\$name" -Filter $filter -Name '.' -AtElement @{ name = $key } -ErrorAction SilentlyContinue
        Add-WebConfigurationProperty -PSPath "IIS:\Sites\$name" -Filter $filter -Name '.' -Value @{ name = $key; value = $vars[$key] }
    }

    $source = @{ 'edc' = 'StudyData EDC'; 'monitor' = 'StudyData Monitor'; 'sponsor-api' = 'StudyData Sponsor API' }[$name]
    if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
        New-EventLog -LogName Application -Source $source
    }

    Start-WebAppPool -Name $name
    Write-Host "$(if ($Http) { 'http' } else { 'https' })://$hostName/ -> $path"
}

if ($Sites -contains 'edc') {
    Write-Host 'Load the study and create site users from the edc folder:'
    Write-Host "  cd $(Join-Path $SitesRoot 'edc'); `$env:ConnectionStrings__StudyData='$connection'"
    Write-Host '  .\StudyData.Edc.Web.exe setup-sample-study'
    Write-Host '  .\StudyData.Edc.Web.exe add-site-user aino.virtanen@hus.example "Aino Virtanen" FAB-ONC-201 101 Investigator --sign'
}
