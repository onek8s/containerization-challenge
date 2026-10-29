<#
.SYNOPSIS
    Creates the MeterBilling database, schema, reference data and logins.
    Needs sqlcmd (SQL Server command line utilities) and sysadmin rights on the server.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -EngineAccount CORP\svc-billing -WebAccount CORP\svc-billing-web -OperatorsGroup CORP\Billing-Operators
#>
param(
    [Parameter(Mandatory)] [string]$SqlServer,
    [Parameter(Mandatory)] [string]$EngineAccount,
    [Parameter(Mandatory)] [string]$WebAccount,
    [Parameter(Mandatory)] [string]$OperatorsGroup,
    [switch]$SkipReferenceData
)

$ErrorActionPreference = 'Stop'
$db = Join-Path (Split-Path -Parent $PSScriptRoot) 'db'

function Invoke-Script([string]$file, [string[]]$variables = @()) {
    Write-Host "Running $file"
    $arguments = @('-S', $SqlServer, '-E', '-b', '-I', '-i', (Join-Path $db $file))
    if ($variables.Count -gt 0) { $arguments += '-v'; $arguments += $variables }
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file failed" }
}

Invoke-Script '01-create-database.sql'
Invoke-Script '02-schema.sql'
if (-not $SkipReferenceData) { Invoke-Script '03-reference-data.sql' }
Invoke-Script '04-logins.sql' @("EngineAccount=$EngineAccount", "WebAccount=$WebAccount", "OperatorsGroup=$OperatorsGroup")
