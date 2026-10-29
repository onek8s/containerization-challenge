#Requires -Version 7.2
<#
.SYNOPSIS
  Installs or updates the IIS sites of the environment (all, or the ones named with -Project).
.DESCRIPTION
  Each site gets its own application pool (No Managed Code, ApplicationPoolIdentity) and an HTTPS binding
  with SNI using the wildcard certificate from LocalMachine\My. The ASP.NET Core hosting bundle
  (3.1, 6.0) must be installed on the host; ANCM runs the apps in-process.
  The site folder is swapped with app_offline.htm so that IIS releases the dlls.
.EXAMPLE
  .\Install-WebSite.ps1 -Environment .\lab.psd1 -Project VanArsdel.Orders.Api
#>
param(
    [Parameter(Mandatory)][string] $Environment,
    [string[]] $Project
)
. "$PSScriptRoot\Common.ps1"
$envData = Import-RetailEnvironment $Environment

$sites = $envData.WebSites | Where-Object { -not $Project -or $Project -contains $_.Project }
foreach ($site in $sites) {
    $hostName = Get-HostName $envData $site.Host
    Write-Host "== $($site.Project) -> https://$($site.HostName) on $hostName" -ForegroundColor Cyan

    $package = Get-PackagePath $site.Project
    $template = Join-Path (Join-Path $PSScriptRoot '..\publish') "$($site.Project)\appsettings.Production.json"
    $settings = New-AppSettingsContent -Environment $envData -Project $site.Project -Template $template
    $folder = Join-Path $envData.InstallRoot "Sites\$($site.Pool)"

    $session = New-PSSession -ComputerName $hostName
    try {
        $thumbprint = Get-CertificateThumbprint -Environment $envData -Session $session
        Copy-PackageToHost -Session $session -Package $package -Target $folder -Settings $settings

        Invoke-Command -Session $session -ArgumentList $site, $folder, $thumbprint, $envData.LogRoot -ScriptBlock {
            param($site, $folder, $thumbprint, $logRoot)
            Import-Module WebAdministration
            $pool = $site.Pool

            if (-not (Test-Path "IIS:\AppPools\$pool")) {
                New-WebAppPool -Name $pool | Out-Null
            }
            Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
            Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
            Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
            Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.loadUserProfile -Value $true
            # Recycle at night instead of every 29 hours in the middle of the day.
            Set-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
            Clear-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.schedule
            New-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' } | Out-Null

            # Take the running site offline while the folders are swapped.
            if (Test-Path $folder) { Set-Content -Path (Join-Path $folder 'app_offline.htm') -Value '<h1>Updating, back in a minute.</h1>' }
            Start-Sleep -Seconds 3
            if (Test-Path "$folder.old") { Remove-Item "$folder.old" -Recurse -Force }
            if (Test-Path $folder) { Rename-Item $folder "$folder.old" }
            Rename-Item "$folder.new" $folder

            $identity = "IIS AppPool\$pool"
            $logDir = Join-Path $logRoot $pool
            New-Item -ItemType Directory -Force -Path $logDir | Out-Null
            & icacls.exe $folder /grant "${identity}:(OI)(CI)RX" | Out-Null
            & icacls.exe $logDir /grant "${identity}:(OI)(CI)M" | Out-Null
            & icacls.exe (Join-Path $folder 'appsettings.Production.json') /inheritance:r /grant:r "BUILTIN\Administrators:(F)" "NT AUTHORITY\SYSTEM:(F)" "${identity}:(R)" | Out-Null

            # Data Protection keys of the Blazor back office and the generated report files (see appsettings.Production.json).
            $writable = @{ 'VanArsdel.BackOffice' = 'Keys\BackOffice'; 'VanArsdel.Reporting.Api' = 'Reports' }
            if ($writable.ContainsKey($site.Project)) {
                $path = Join-Path (Split-Path (Split-Path $folder -Parent) -Parent) $writable[$site.Project]
                New-Item -ItemType Directory -Force -Path $path | Out-Null
                & icacls.exe $path /grant "${identity}:(OI)(CI)M" | Out-Null
            }

            if (-not (Test-Path "IIS:\Sites\$($site.Site)")) {
                New-Website -Name $site.Site -PhysicalPath $folder -ApplicationPool $pool -HostHeader $site.HostName -Port 443 -Ssl -SslFlags 1 | Out-Null
                Get-WebBinding -Name $site.Site -Protocol 'http' -ErrorAction SilentlyContinue | Remove-WebBinding
            }
            else {
                Set-ItemProperty "IIS:\Sites\$($site.Site)" -Name physicalPath -Value $folder
            }
            Set-ItemProperty "IIS:\Sites\$($site.Site)" -Name applicationDefaults.preloadEnabled -Value $true
            $binding = Get-WebBinding -Name $site.Site -Protocol https -HostHeader $site.HostName
            $binding.AddSslCertificate($thumbprint, 'My')

            Restart-WebAppPool -Name $pool
        }
    }
    finally {
        Remove-PSSession $session
    }
    $healthPath = if ($site.ContainsKey('HealthPath')) { $site.HealthPath } else { '/health' }
    Wait-HealthEndpoint "https://$($site.HostName)$healthPath"
}
