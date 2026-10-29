<#
.SYNOPSIS
    Copies the SSIS package used by the "Trade Rebates - Import sell-out workbooks" SQL Server Agent job.
.DESCRIPTION
    The job runs the package from the file system (dtexec /FILE) under the TradeRebatesFiles proxy, so
    Integration Services and the Microsoft Access Database Engine 2016 (x64) must be installed on the database server.
#>
[CmdletBinding()]
param(
    [string] $SsisPath = 'D:\TradeRebates\Ssis',
    [string] $ProxyAccount = 'CORP\svc-rebates-files'
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Service -Name 'MsDtsServer160' -ErrorAction SilentlyContinue)) {
    Write-Warning 'SQL Server Integration Services 16.0 is not installed on this server.'
}
New-Item -ItemType Directory -Path $SsisPath -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot '..\ssis\SellOutWorkbookImport.dtsx') $SsisPath -Force
& icacls.exe $SsisPath /grant "${ProxyAccount}:(OI)(CI)RX" /Q | Out-Null
Write-Host "Package copied to $SsisPath."
