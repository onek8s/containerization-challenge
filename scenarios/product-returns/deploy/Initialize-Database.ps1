<#
.SYNOPSIS
    Creates the ProductReturns database, the rtn and erp schemas and the Windows logins of the
    service accounts. Requires sqlcmd and sysadmin rights on the SQL Server instance.
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -Domain CORP
#>
param(
    [string]$SqlServer = 'sql01.corp.local',
    [string]$DatabaseName = 'ProductReturns',
    [string]$Domain = 'CORP'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

& sqlcmd -S $SqlServer -E -b -Q "IF DB_ID(N'$DatabaseName') IS NULL CREATE DATABASE [$DatabaseName];"
if ($LASTEXITCODE -ne 0) { throw "Creating database $DatabaseName failed." }

foreach ($script in Get-ChildItem (Join-Path $here 'db') -Filter '*.sql' | Sort-Object Name) {
    Write-Host "Running $($script.Name)"
    & sqlcmd -S $SqlServer -E -b -d $DatabaseName -i $script.FullName -v Domain=$Domain DatabaseName=$DatabaseName
    if ($LASTEXITCODE -ne 0) { throw "$($script.Name) failed." }
}
