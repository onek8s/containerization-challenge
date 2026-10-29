<#
.SYNOPSIS
    Full deployment in the usual order. On the multi-server production layout run the steps on their servers instead:
    database from any admin workstation, web sites on web01/web02/app01, services on clinic-int, reports from any workstation.
.EXAMPLE
    .\Deploy-Clinic.ps1 -Environment Lab
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Lab',
    [string] $SecretsFile,
    [ValidateSet('Dns', 'Database', 'Publish', 'WebSites', 'Services', 'Reports')] [string[]] $Steps = @('Database', 'Publish', 'WebSites', 'Services', 'Reports')
)
$ErrorActionPreference = 'Stop'
if ($Steps -contains 'Dns')      { & (Join-Path $PSScriptRoot 'Register-Dns.ps1') -Environment $Environment }
if ($Steps -contains 'Database') { & (Join-Path $PSScriptRoot 'Install-Database.ps1') -Environment $Environment -SecretsFile $SecretsFile }
if ($Steps -contains 'Publish')  { & (Join-Path $PSScriptRoot 'Publish-Applications.ps1') -Environment $Environment -SecretsFile $SecretsFile }
if ($Steps -contains 'WebSites') { & (Join-Path $PSScriptRoot 'Install-WebSites.ps1') -Environment $Environment }
if ($Steps -contains 'Services') { & (Join-Path $PSScriptRoot 'Install-Services.ps1') -Environment $Environment }
if ($Steps -contains 'Reports')  { & (Join-Path $PSScriptRoot 'Publish-Reports.ps1') -Environment $Environment -SecretsFile $SecretsFile }
