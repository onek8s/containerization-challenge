<#
.SYNOPSIS
    Creates the AccountStatements database, schema, reference and test data, logins and permissions.
    Needs sqlcmd (SQL Server command line tools) and sysadmin rights on the SQL Server.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -PortalAccount CORP\svc-stm-web -ApiAccount CORP\svc-stm-api -DistributionAccount CORP\svc-stm-dist
#>
param(
    [string]$SqlServer = 'sql01.corp.local',
    [Parameter(Mandatory)] [string]$PortalAccount,
    [Parameter(Mandatory)] [string]$ApiAccount,
    [Parameter(Mandatory)] [string]$DistributionAccount,
    [string]$FileServer = 'files01.corp.local',
    [switch]$SkipReferenceData
)

$ErrorActionPreference = 'Stop'
$db = Join-Path (Split-Path -Parent $PSScriptRoot) 'db'

function Invoke-Script([string]$file, [string]$database = 'master', [string[]]$variables = @()) {
    Write-Host "Running $file"
    $arguments = @('-S', $SqlServer, '-E', '-b', '-C', '-d', $database, '-i', (Join-Path $db $file))
    if ($variables.Count -gt 0) { $arguments += '-v'; $arguments += $variables }
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file failed" }
}

Invoke-Script '01-create-database.sql'
Invoke-Script '02-schema.sql' 'AccountStatements'
if (-not $SkipReferenceData) {
    Invoke-Script '03-reference-data.sql' 'AccountStatements' @("StatementShare=\\$FileServer\Statements$", "FinanceShare=\\$FileServer\Finance$")
}
Invoke-Script '04-logins.sql' 'master' @("PortalAccount=$PortalAccount", "ApiAccount=$ApiAccount", "DistributionAccount=$DistributionAccount")
Write-Host 'Database ready.'
