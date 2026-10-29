<#
.SYNOPSIS
    Creates the DealerOrders database on sql01 and runs db\*.sql in order (sqlcmd, Windows authentication as a sysadmin).
.EXAMPLE
    $p = @{ PortalPassword = (Read-Host -AsSecureString portal); DeskPassword = ...; DocsPassword = ...; AxConnectorPassword = ...;
            PriceSyncPassword = ...; EdiPassword = ...; NotifyPassword = ... }
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -Passwords $p
#>
[CmdletBinding()]
param(
    [string]$SqlServer = 'sql01.corp.local',
    [Parameter(Mandatory)][hashtable]$Passwords,
    [string]$ScriptFolder = (Join-Path (Split-Path $PSScriptRoot -Parent) 'db')
)

. "$PSScriptRoot\common.ps1"

if (-not (Get-Command sqlcmd.exe -ErrorAction SilentlyContinue)) { throw 'sqlcmd.exe not found (SQL Server command line utilities).' }
foreach ($name in 'PortalPassword', 'DeskPassword', 'DocsPassword', 'AxConnectorPassword', 'PriceSyncPassword', 'EdiPassword', 'NotifyPassword') {
    if (-not $Passwords.ContainsKey($name)) { throw "Passwords.$name is missing." }
}

foreach ($file in Get-ChildItem $ScriptFolder -Filter '*.sql' | Sort-Object Name) {
    Write-Step $file.Name
    $vars = if ($file.Name -like '02-*') { $Passwords } else { @{} }
    Invoke-SqlScript -Server $SqlServer -File $file.FullName -Variables $vars
}

Write-Step 'Check'
& sqlcmd.exe -S $SqlServer -E -d DealerOrders -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables;"
