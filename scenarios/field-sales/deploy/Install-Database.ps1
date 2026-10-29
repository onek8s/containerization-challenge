<#
.SYNOPSIS
    Creates the FieldSales database, objects, demo data and the application login. Needs sqlcmd
    (SQL Server command line utilities) and sysadmin rights; the instance must allow SQL Server authentication.
.EXAMPLE
    .\Install-Database.ps1 -SqlServer sql01.corp.local -AppLogin fieldsales_app
#>
param(
    [Parameter(Mandatory)] [string]$SqlServer,
    [string]$AppLogin = 'fieldsales_app',
    [SecureString]$AppPassword,
    [switch]$SkipDemoData
)

$ErrorActionPreference = 'Stop'
$db = Join-Path (Split-Path $PSScriptRoot -Parent) 'db'
if (-not $AppPassword) { $AppPassword = Read-Host "Password for SQL login $AppLogin" -AsSecureString }
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($AppPassword))

function Invoke-Script([string]$file, [string]$database, [string[]]$variables) {
    Write-Host "Running $file"
    $arguments = @('-S', $SqlServer, '-E', '-b', '-I', '-f', '65001', '-i', (Join-Path $db $file))
    if ($database) { $arguments += '-d', $database }
    foreach ($v in $variables) { $arguments += '-v', $v }
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file failed." }
}

Invoke-Script '01-create-database.sql' $null @()
$exists = & sqlcmd -S $SqlServer -E -d FieldSales -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name = 'SalesOrder'"
if ($exists.Trim() -eq '0') { Invoke-Script '02-schema.sql' 'FieldSales' @() } else { Write-Host 'Schema exists, skipping 02-schema.sql' }
Invoke-Script '03-procedures.sql' 'FieldSales' @()
if (-not $SkipDemoData -and $exists.Trim() -eq '0') { Invoke-Script '04-seed.sql' 'FieldSales' @() }
Invoke-Script '05-login.sql' $null @("AppLogin=$AppLogin", "AppPassword=$plain")

Write-Host "Database ready. Connection string for the sites:"
Write-Host "  Data Source=$SqlServer;Initial Catalog=FieldSales;User ID=$AppLogin;Password=...;MultipleActiveResultSets=True"
