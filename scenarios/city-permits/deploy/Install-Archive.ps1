<#
.SYNOPSIS
    Enables FILESTREAM on the SQL Server instance and creates the case document FileTable.
.DESCRIPTION
    Run on permits-sql. Requires SQL Server 2019/2022 Standard or Enterprise on Windows with
    Full-Text Search and the Adobe PDF iFilter 64 installed. The FileTable share
    \\permits-sql\PERMITSFS\PermitsArchive\CaseDocuments is used by the web apps, the jobs service,
    the scanning stations and the RecordsOne server (capture by reference).
#>
param(
    [string] $Instance = 'MSSQLSERVER',
    [string] $ShareName = 'PERMITSFS',
    [string[]] $ShareReaders = @('CORP\svc-permits-web', 'CORP\svc-permits-app', 'CORP\svc-recordsone'),
    [string] $IFilterPath = 'C:\Program Files\Adobe\Adobe PDF iFilter 64 for 64-bit platforms\bin'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator

$wmi = Get-WmiObject -Namespace 'root\Microsoft\SqlServer\ComputerManagement16' -Class FilestreamSettings | Where-Object { $_.InstanceName -eq $Instance }
$wmi.EnableFilestream(3, $ShareName) | Out-Null
Restart-Service "MSSQLSERVER" -Force
if (Test-Path $IFilterPath) { $env:PATH += ";$IFilterPath" ; [Environment]::SetEnvironmentVariable('PATH', $env:PATH, 'Machine') }
else { Write-Warning 'PDF iFilter not found; full-text search will not index PDF content.' }

Invoke-Sqlcmd -ServerInstance localhost -InputFile (Join-Path $PSScriptRoot 'sql\03-filetable.sql')
foreach ($r in $ShareReaders) { Write-Host "Grant $r access to permits.CaseDocuments via 04-logins.sql (FileTable share uses SQL permissions)." }
