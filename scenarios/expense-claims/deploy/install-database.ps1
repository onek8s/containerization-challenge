<#
.SYNOPSIS
    Creates the ExpenseClaims database, the SQL logins of the API and the jobs host, the Quartz
    scheduler tables and the application schema.
.DESCRIPTION
    Run as a sysadmin of the SQL Server instance. Requires the SqlServer PowerShell module
    (Install-Module SqlServer). The SQL Server instance must allow SQL Server authentication
    (mixed mode). The schema script is idempotent and can be re-run after an upgrade.
.EXAMPLE
    .\install-database.ps1 -SqlInstance expenses-sql.corp.local `
        -AppPassword (Read-Host -AsSecureString 'expenses_app') -JobsPassword (Read-Host -AsSecureString 'expenses_jobs')
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'expenses-sql.corp.local',
    [string] $DatabaseName = 'ExpenseClaims',
    [Parameter(Mandatory)] [securestring] $AppPassword,
    [Parameter(Mandatory)] [securestring] $JobsPassword,
    [switch] $TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
Import-Module SqlServer

function ConvertTo-Plain([securestring] $Secure) {
    [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure))
}

$db = Join-Path $PSScriptRoot '..\db'
$common = @{ ServerInstance = $SqlInstance; Encrypt = 'Optional'; TrustServerCertificate = $TrustServerCertificate.IsPresent; QueryTimeout = 600 }

Write-Host "Creating database $DatabaseName and logins on $SqlInstance"
Invoke-Sqlcmd @common -Database master -InputFile (Join-Path $db '01-create-database.sql') -Variable @(
    "DatabaseName=$DatabaseName",
    "AppPassword=$((ConvertTo-Plain $AppPassword).Replace("'", "''"))",
    "JobsPassword=$((ConvertTo-Plain $JobsPassword).Replace("'", "''"))"
)

Write-Host 'Creating Quartz scheduler tables'
Invoke-Sqlcmd @common -Database $DatabaseName -InputFile (Join-Path $db '02-quartz-tables.sql')

Write-Host 'Applying application schema'
Invoke-Sqlcmd @common -Database $DatabaseName -InputFile (Join-Path $db '03-expenseclaims-schema.sql')

$applied = Invoke-Sqlcmd @common -Database $DatabaseName -Query 'SELECT MigrationId FROM exp.__EFMigrationsHistory ORDER BY MigrationId'
$applied | ForEach-Object { Write-Host "  migration $($_.MigrationId)" }
Write-Host 'Database ready. Run 04-reference-data.sql after the first HR import.'
