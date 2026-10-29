<#
.SYNOPSIS
    Creates or updates the Fabrikam Housing database on sql01 with sqlcmd.
.DESCRIPTION
    Runs db\01..05 in order. The scripts are idempotent except 02-housing-schema.sql and 05-identity-schema.sql,
    which are only run when the schema does not exist yet.
    Prerequisite: sqlcmd (SQL Server command line utilities) on the machine running the script.
.EXAMPLE
    .\install-database.ps1 -SqlInstance sql01.corp.local -IdentityPassword (Read-Host -AsSecureString) -PortalPassword (Read-Host -AsSecureString) -ApiPassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [string] $DataPath = 'E:\SQLData',
    [string] $LogPath = 'F:\SQLLogs',
    [Parameter(Mandatory)] [securestring] $IdentityPassword,
    [Parameter(Mandatory)] [securestring] $PortalPassword,
    [Parameter(Mandatory)] [securestring] $ApiPassword
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force
$db = Join-Path (Get-HousingSourceRoot) 'db'

function Invoke-Script([string] $file, [string[]] $variables = @(), [string] $database = 'master') {
    Write-Host "Running $file"
    $sqlArgs = @('-S', $SqlInstance, '-E', '-b', '-d', $database, '-i', (Join-Path $db $file))
    if ($variables.Count) { $sqlArgs += '-v'; $sqlArgs += $variables }
    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "$file failed" }
}

function Test-Table([string] $schema, [string] $table) {
    $result = & sqlcmd -S $SqlInstance -E -b -d $DatabaseName -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = '$schema' AND t.name = '$table'"
    return ([int]($result | Select-Object -First 1)) -gt 0
}

Invoke-Script '01-create-database.sql' @("DatabaseName=$DatabaseName", "DataPath=$DataPath", "LogPath=$LogPath")
if (-not (Test-Table 'housing' 'Properties')) { Invoke-Script '02-housing-schema.sql' -database $DatabaseName }
Invoke-Script '03-reference-data.sql' -database $DatabaseName
Invoke-Script '04-logins-and-users.sql' @(
    "DatabaseName=$DatabaseName",
    "IdentityPassword=$(ConvertTo-PlainText $IdentityPassword)",
    "PortalPassword=$(ConvertTo-PlainText $PortalPassword)",
    "ApiPassword=$(ConvertTo-PlainText $ApiPassword)")
if (-not (Test-Table 'auth' 'AspNetUsers')) { Invoke-Script '05-identity-schema.sql' -database $DatabaseName }

Write-Host "Database $DatabaseName on $SqlInstance is ready."
