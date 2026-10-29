<#
.SYNOPSIS
    Creates or updates the OrderHub database, its SQL logins and the product list.
.DESCRIPTION
    Runs db\01-Schema.sql, db\02-Logins.sql and db\03-Products.sql with sqlcmd. The account running the
    script (or -AdminUser) must be sysadmin. Mixed mode authentication must be enabled on the instance.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -ServicePassword (Read-Host -AsSecureString) `
        -PortalPassword (Read-Host -AsSecureString) -ReportsPassword (Read-Host -AsSecureString)
#>
param(
    [Parameter(Mandatory)] [string] $SqlServer,
    [string] $DatabaseName = 'OrderHub',
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [Parameter(Mandatory)] [securestring] $PortalPassword,
    [Parameter(Mandatory)] [securestring] $ReportsPassword,
    [string] $AdminUser,
    [securestring] $AdminPassword,
    [switch] $SkipProducts
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$db = Join-Path (Split-Path $PSScriptRoot -Parent) 'db'
if (-not (Test-Path $db)) { $db = Join-Path $PSScriptRoot '..\db' }

# @( ) keeps a one-element list an array; splatting a plain string passes its characters.
$auth = @(if ($AdminUser) { '-U', $AdminUser, '-P', (ConvertTo-PlainText $AdminPassword) } else { '-E' })
$vars = @(
    '-v', "DatabaseName=$DatabaseName",
    "ServicePassword=$(ConvertTo-PlainText $ServicePassword)",
    "PortalPassword=$(ConvertTo-PlainText $PortalPassword)",
    "ReportsPassword=$(ConvertTo-PlainText $ReportsPassword)"
)

$scripts = @('01-Schema.sql', '02-Logins.sql')
if (-not $SkipProducts) { $scripts += '03-Products.sql' }

foreach ($script in $scripts) {
    Write-Host "Running $script on $SqlServer"
    & sqlcmd.exe -S $SqlServer @auth -b -I -i (Join-Path $db $script) @vars
    if ($LASTEXITCODE -ne 0) { throw "$script failed." }
}
