<#
.SYNOPSIS
    Creates or updates the SupplierInvoices database and grants the service accounts. Run as a SQL Server sysadmin.
    With -ErpLabDatabase also creates the ERP test database (test and lab environments only).
.EXAMPLE
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -GatewayAccount CORP\svc-inv-gw -ProcessorAccount CORP\svc-inv-proc -ErpLinkAccount CORP\svc-inv-erp -WebAccount CORP\svc-inv-web
#>
param(
    [string]$SqlServer = 'sql01.corp.local',
    [Parameter(Mandatory)] [string]$GatewayAccount,
    [Parameter(Mandatory)] [string]$ProcessorAccount,
    [Parameter(Mandatory)] [string]$ErpLinkAccount,
    [Parameter(Mandatory)] [string]$WebAccount,
    [switch]$ErpLabDatabase,
    [string]$ErpSqlServer = 'erp01.corp.local'
)

$ErrorActionPreference = 'Stop'
$db = Join-Path $PSScriptRoot '..\db'

function Invoke-Script([string]$Server, [string]$Database, [string]$File, [string[]]$Variables = @()) {
    Write-Host "$Server/$Database <- $(Split-Path $File -Leaf)"
    $sqlArgs = @('-S', $Server, '-E', '-b', '-I', '-d', $Database, '-i', $File)
    if ($Variables.Count -gt 0) { $sqlArgs += '-v'; $sqlArgs += $Variables }
    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "$File failed." }
}

Invoke-Script $SqlServer 'master' "$db\01-create-database.sql"
$exists = & sqlcmd -S $SqlServer -E -d SupplierInvoices -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name = 'Invoice' AND schema_id = SCHEMA_ID('inv')"
if ($exists.Trim() -eq '0') {
    Invoke-Script $SqlServer 'SupplierInvoices' "$db\02-schema.sql"
}
Invoke-Script $SqlServer 'SupplierInvoices' "$db\03-procedures.sql"
Invoke-Script $SqlServer 'SupplierInvoices' "$db\04-reference-data.sql"
Invoke-Script $SqlServer 'SupplierInvoices' "$db\05-logins.sql" @(
    "GatewayAccount=$GatewayAccount", "ProcessorAccount=$ProcessorAccount", "ErpLinkAccount=$ErpLinkAccount", "WebAccount=$WebAccount")

if ($ErpLabDatabase) {
    Invoke-Script $ErpSqlServer 'master' "$db\erp-lab\WingtipErp.sql" @("ErpLinkAccount=$ErpLinkAccount")
}
