<#
.SYNOPSIS
    Creates the ProjectSitesHub database (add-in KPI history and sync state), the SQL logins and the
    SQL Agent purge job. Run on a machine with sqlcmd as a sysadmin of sql01.
.EXAMPLE
    .\Initialize-Database.ps1
#>
param(
    [string] $Version,
    [securestring] $DashboardsPassword = (Read-Host -AsSecureString 'Password for SQL login ps_dashboards'),
    [securestring] $SyncPassword = (Read-Host -AsSecureString 'Password for SQL login ps_sync'),
    [switch] $SkipAgentJob
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
$settings = Get-ProjectSitesSettings
$db = if ($Version) { Join-Path (Get-ProjectSitesDrop -Settings $settings -Version $Version) 'db' } else { Join-Path (Split-Path -Parent $PSScriptRoot) 'db' }

function Plain([securestring] $s) {
    [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
}

Write-Step "Schema on $($settings.SqlServer)"
Invoke-SqlScript -Server $settings.SqlServer -Path (Join-Path $db '01-schema.sql')

Write-Step 'Logins'
Invoke-SqlScript -Server $settings.SqlServer -Path (Join-Path $db '02-logins.sql') -Variables @(
    "DashboardsPassword=$(Plain $DashboardsPassword)", "SyncPassword=$(Plain $SyncPassword)")

if (-not $SkipAgentJob) {
    Write-Step 'SQL Agent job'
    Invoke-SqlScript -Server $settings.SqlServer -Path (Join-Path $db '03-agent-jobs.sql')
}
Write-Step 'Put the passwords into the connection strings of the add-in Web.config and the sync service config.'
