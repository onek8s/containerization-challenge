<#
.SYNOPSIS
    Creates the application pools and sites in IIS 7.5 and copies the published files (run on each web server,
    after Publish-Applications.ps1). -Sites limits the run to the sites hosted on this server.
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production',
    [string[]] $Sites,
    [string] $StagingRoot = (Join-Path $PSScriptRoot 'out')
)
. (Join-Path $PSScriptRoot 'Common.ps1')
Import-Module WebAdministration
$settings = Get-DeploySettings $Environment $null
$staging = Join-Path $StagingRoot $settings.Name
$sitesRoot = New-Folder (Get-LocalPath $settings.SitesRoot)

foreach ($site in $settings.Sites) {
    if ($Sites -and $Sites -notcontains $site.Name) { continue }
    $hostName = "$($site.Host).$($settings.DnsZone)"
    $physical = Join-Path $sitesRoot $site.Host
    Write-Step "$($site.Name) ($hostName)"

    $poolPath = "IIS:\AppPools\$($site.Pool)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $site.Pool | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value $site.Runtime
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 0          # Integrated
    Set-ItemProperty $poolPath -Name processModel.identityType -Value 4   # ApplicationPoolIdentity
    Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value '00:00:00'
    Clear-ItemProperty $poolPath -Name recycling.periodicRestart.schedule
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' }

    Stop-WebAppPool -Name $site.Pool -ErrorAction SilentlyContinue
    New-Folder $physical | Out-Null
    & robocopy.exe (Join-Path $staging "sites\$($site.Project)") $physical /MIR /XD logs App_Data\errors /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $($site.Project) failed (robocopy $LASTEXITCODE)." }
    Grant-FolderAccess (Join-Path $physical 'App_Data') "IIS AppPool\$($site.Pool)"
    Grant-FolderAccess (Join-Path $sitesRoot 'logs') "IIS AppPool\$($site.Pool)"

    if (-not (Test-Path "IIS:\Sites\$($site.Name)")) {
        New-Website -Name $site.Name -PhysicalPath $physical -ApplicationPool $site.Pool -HostHeader $hostName -Port 80 | Out-Null
    }
    else {
        Set-ItemProperty "IIS:\Sites\$($site.Name)" -Name physicalPath -Value $physical
        Set-ItemProperty "IIS:\Sites\$($site.Name)" -Name applicationPool -Value $site.Pool
    }

    if ($settings.CertificateThumbprint) {
        if (-not (Get-WebBinding -Name $site.Name -Protocol https)) {
            New-WebBinding -Name $site.Name -Protocol https -Port 443 -IPAddress $settings.Hosts[$site.Host] | Out-Null
        }
        $bindingPath = "IIS:\SslBindings\$($settings.Hosts[$site.Host])!443"
        if (-not (Test-Path $bindingPath)) {
            Get-Item "Cert:\LocalMachine\My\$($settings.CertificateThumbprint)" | New-Item $bindingPath | Out-Null
        }
    }

    # WCF activation for the .svc endpoints (HTTP activation feature must be installed on the server).
    if ($site.Project -eq 'Southridge.Clinic.Services') {
        & "$env:windir\Microsoft.NET\Framework\v4.0.30319\ServiceModelReg.exe" -ia -q | Out-Null
    }

    Start-WebAppPool -Name $site.Pool
}
