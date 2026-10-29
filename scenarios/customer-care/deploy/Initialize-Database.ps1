<#
.SYNOPSIS
    Creates the CustomerCare database, schema, procedures, reference data, demo data and logins. Run as a SQL Server sysadmin.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -WebAccount CORP\svc-care-web -ApiAccount CORP\svc-care-api -ToolsAccount CORP\svc-care-tools
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -WebAccount 'IIS APPPOOL\care' -ApiAccount 'IIS APPPOOL\care-api' -ToolsAccount "$env:COMPUTERNAME\svc-care-tools" -SkipDemoData
#>
param(
    [Parameter(Mandatory)] [string]$SqlServer,
    [Parameter(Mandatory)] [string]$WebAccount,
    [Parameter(Mandatory)] [string]$ApiAccount,
    [Parameter(Mandatory)] [string]$ToolsAccount,
    [switch]$SkipDemoData
)

$ErrorActionPreference = 'Stop'
$db = Join-Path $PSScriptRoot '..\db'

function Invoke-Script([string]$file, [string]$database = 'master', [string[]]$variables = @()) {
    Write-Host "Running $file"
    $sqlArgs = @('-S', $SqlServer, '-E', '-b', '-d', $database, '-i', (Join-Path $db $file))
    foreach ($v in $variables) { $sqlArgs += @('-v', $v) }
    & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "$file failed." }
}

Invoke-Script '01-create-database.sql'
Invoke-Script '02-schema.sql' 'CustomerCare'
Invoke-Script '03-procedures.sql' 'CustomerCare'
Invoke-Script '04-reference-data.sql' 'CustomerCare'
if (-not $SkipDemoData) { Invoke-Script '05-demo-data.sql' 'CustomerCare' }
Invoke-Script '06-logins.sql' 'master' @("WebAccount=$WebAccount", "ApiAccount=$ApiAccount", "ToolsAccount=$ToolsAccount")

Write-Host 'Database ready.'
