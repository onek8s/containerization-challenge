<#
.SYNOPSIS
    Creates the project hub site collection (project register, site requests, staff directory, status
    roll-up) in its own content database and activates the hub features.
.EXAMPLE
    .\New-ProjectHub.ps1 -OwnerAlias CORP\spadmin -SecondaryOwnerAlias CORP\gg-collab-admins
#>
param(
    [string] $OwnerAlias,
    [string] $SecondaryOwnerAlias = 'CORP\svc-ps-farm'
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Assert-SharePointShell
$settings = Get-ProjectSitesSettings
if (-not $OwnerAlias) { $OwnerAlias = $settings.FarmAdmin }

$webApp = Get-SPWebApplication -Identity $settings.WebApplicationUrl
if (-not (Get-SPContentDatabase -WebApplication $webApp | Where-Object { $_.Name -eq $settings.HubContentDatabase })) {
    Write-Step "Creating content database $($settings.HubContentDatabase)"
    New-SPContentDatabase -Name $settings.HubContentDatabase -DatabaseServer $settings.ContentDatabaseServer -WebApplication $webApp `
        -MaxSiteCount 1 -WarningSiteCount 0 | Out-Null
}

$site = Get-SPSite -Identity $settings.HubUrl -ErrorAction SilentlyContinue
if (-not $site) {
    Write-Step "Creating $($settings.HubUrl)"
    $site = New-SPSite -Url $settings.HubUrl -Name 'Projects' -Description 'Lucerne Engineering project register' `
        -Template 'STS#0' -Language 1033 -OwnerAlias $OwnerAlias -SecondaryOwnerAlias $SecondaryOwnerAlias `
        -ContentDatabase $settings.HubContentDatabase
}

Enable-ProjectSitesFeature -Identity 'A3B1D6E2-0F4C-4B7A-9D85-1E2F3A4B5C61' -Url $settings.HubUrl   # columns and content types
Enable-ProjectSitesFeature -Identity 'E7F5BAC6-4D80-4FBE-B1C9-5C6D7E8F9AA5' -Url $settings.HubUrl   # web parts
Enable-ProjectSitesFeature -Identity 'B4C2E7F3-1A5D-4C8B-8E96-2F3A4B5C6D72' -Url $settings.HubUrl   # hub lists and pages

# Everyone in the company reads the register; the all-staff claim comes from the custom claims provider.
$web = $site.RootWeb
try {
    $visitors = $web.AssociatedVisitorGroup
    $allStaff = $web.EnsureUser('c:0-.t|lucerne|allstaff')
    $visitors.AddUser($allStaff)
    Write-Step 'All staff added to the hub visitors group'
} finally {
    $web.Dispose()
    $site.Dispose()
}
