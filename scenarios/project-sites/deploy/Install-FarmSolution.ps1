<#
.SYNOPSIS
    Adds or upgrades the Lucerne Project Sites farm solutions and activates the farm and web application
    features. Run in the SharePoint 2016 Management Shell on a farm server as the farm admin.
.DESCRIPTION
    First install: Add-SPSolution + Install-SPSolution (GAC deployment, web application scoped resources).
    Upgrade: Update-SPSolution when the solution id is already in the farm. Feature definitions that changed
    are upgraded with -Force activation (the feature receivers are idempotent).
.EXAMPLE
    .\Install-FarmSolution.ps1 -Version 5.3.1
#>
param(
    [string] $Version,
    [switch] $SkipClaims
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Assert-SharePointShell
$settings = Get-ProjectSitesSettings
$drop = Get-ProjectSitesDrop -Settings $settings -Version $Version
$webApp = Get-SPWebApplication -Identity $settings.WebApplicationUrl

$packages = @('Lucerne.ProjectSites.wsp')
if (-not $SkipClaims) { $packages += 'Lucerne.ProjectSites.Claims.wsp' }

foreach ($name in $packages) {
    $path = Join-Path $drop "farm\$name"
    $existing = Get-SPSolution -Identity $name -ErrorAction SilentlyContinue
    if ($existing -and $existing.Deployed) {
        Write-Step "Upgrading $name"
        Update-SPSolution -Identity $name -LiteralPath $path -GACDeployment -Force
    } else {
        if (-not $existing) {
            Write-Step "Adding $name"
            Add-SPSolution -LiteralPath $path | Out-Null
        }
        Write-Step "Deploying $name"
        if ((Get-SPSolution -Identity $name).ContainsWebApplicationResource) {
            Install-SPSolution -Identity $name -GACDeployment -WebApplication $webApp -Force
        } else {
            Install-SPSolution -Identity $name -GACDeployment -Force
        }
    }
    Wait-SolutionJob -Name $name
}

# Web application properties read by the timer jobs, receivers and application pages.
Write-Step 'Setting web application properties'
$webApp.Properties['lucerne.projectsites.huburl'] = $settings.HubUrl
$webApp.Properties['lucerne.projectsites.recordsmanagers'] = $settings.RecordsManagers
$webApp.Update()

# Managed path for project site collections (https://projects.corp.local/projects/p19041).
if (-not (Get-SPManagedPath -WebApplication $webApp -Identity $settings.ProjectManagedPath -ErrorAction SilentlyContinue)) {
    New-SPManagedPath -RelativeURL $settings.ProjectManagedPath -WebApplication $webApp | Out-Null
    Write-Step "Added wildcard managed path /$($settings.ProjectManagedPath)"
}

# Farm and web application scoped features. Site and web scoped features are activated by New-ProjectHub.ps1
# (hub) and by the provisioning timer job (project sites, through the web template).
Enable-ProjectSitesFeature -Identity '4C7E3A52-91D8-4F0B-A6E1-2B9D58C0F713'                                 # web template (farm)
Enable-ProjectSitesFeature -Identity 'D6E4A9B5-3C7F-4EAD-A0B8-4B5C6D7E8F94' -Url $settings.WebApplicationUrl -Force # timer jobs

if (-not $SkipClaims) {
    Enable-ProjectSitesFeature -Identity 'F8A6CBD7-5E91-40CF-82DA-6D7E8F9AABB6'                              # claims provider (farm)
    Enable-ProjectSitesFeature -Identity '09B7DCE8-6FA2-41D0-93EB-7E8F9AABBCC7' -Url $settings.WebApplicationUrl # directory cache job
    Write-Step 'Starting the directory cache job once so the people picker has data'
    Get-SPTimerJob -WebApplication $webApp | Where-Object { $_.Name -eq 'lucerne-claims-directory-cache' } | Start-SPTimerJob
}

Write-Step 'Timer jobs on the web application:'
Get-SPTimerJob -WebApplication $webApp | Where-Object { $_.Name -like 'lucerne-*' } |
    Format-Table Name, Schedule, LastRunTime -AutoSize | Out-Host
