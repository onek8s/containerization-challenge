<#
.SYNOPSIS
    Creates or updates the DocControl database on the SQL Server and grants the service accounts.
    Requires the SqlServer PowerShell module. Run as a SQL Server administrator.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -Domain CORP
#>
param(
    [string]$SqlServer = 'sql01.corp.local',
    [string]$Database = 'DocControl',
    [string]$Domain = 'CORP',
    [switch]$SkipReferenceData
)

$ErrorActionPreference = 'Stop'
Import-Module SqlServer

$scripts = Join-Path $PSScriptRoot 'db'
$variables = @(
    "DatabaseName=$Database",
    "WebAccount=$Domain\svc-dc-web",
    "PackagerAccount=$Domain\svc-dc-packager",
    "IndexerAccount=$Domain\svc-dc-indexer",
    "ReportingGroup=$Domain\DocControl-Controllers"
)

Invoke-Sqlcmd -ServerInstance $SqlServer -Database master -TrustServerCertificate -Query "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"

foreach ($file in Get-ChildItem $scripts -Filter *.sql | Sort-Object Name) {
    if ($SkipReferenceData -and $file.Name -like '*reference*') { continue }
    Write-Host "Running $($file.Name)"
    Invoke-Sqlcmd -ServerInstance $SqlServer -Database $Database -TrustServerCertificate -InputFile $file.FullName -Variable $variables
}
