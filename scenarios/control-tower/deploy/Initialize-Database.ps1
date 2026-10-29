<#
.SYNOPSIS
    Creates the Control Tower databases, schema and logins on SQL Server.
.DESCRIPTION
    Runs db\00-Database.sql, 01-Schema.sql and 02-Logins.sql with sqlcmd as the current Windows user
    (who must be sysadmin). FILESTREAM must be enabled for the instance first.
.EXAMPLE
    $pw = @{ Booking='..'; Portal='..'; Tower='..'; Scan='..'; Gateway='..'; Events='..'; Telematics='..'; Jobs='..'; Identity='..' }
    .\Initialize-Database.ps1 -SqlServer sql01.corp.local -DataPath E:\SQLData -LogPath F:\SQLLogs -Passwords $pw -Seed -LabToolsPath C:\Build\ControlTower\ControlTower.LabTools
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SqlServer,
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory)][string]$LogPath,
    [string]$FileStreamPath,
    [Parameter(Mandatory)][hashtable]$Passwords,
    [string]$DbScriptPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'db'),
    # Loads sample master data and shipments (test and training environments).
    [switch]$Seed,
    [string]$LabToolsPath
)

. "$PSScriptRoot\common.ps1"

if (-not $FileStreamPath) { $FileStreamPath = Join-Path $DataPath 'ControlTowerDocuments' }
foreach ($key in 'Booking', 'Portal', 'Tower', 'Scan', 'Gateway', 'Events', 'Telematics', 'Jobs', 'Identity') {
    if (-not $Passwords.ContainsKey($key)) { throw "Passwords must contain '$key'." }
}

$sqlcmd = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
if (-not $sqlcmd) { throw 'sqlcmd.exe was not found. Install the SQL Server command line utilities.' }

function Invoke-SqlScript([string]$file, [string[]]$variables) {
    Write-Host "  $file"
    $sqlArgs = @('-S', $SqlServer, '-E', '-b', '-I', '-i', (Join-Path $DbScriptPath $file))
    foreach ($v in $variables) { $sqlArgs += @('-v', $v) }
    & $sqlcmd.Source @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "$file failed ($LASTEXITCODE)." }
}

Write-Step 'FILESTREAM configuration'
$level = & $sqlcmd.Source -S $SqlServer -E -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('FilestreamConfiguredLevel') AS int)"
if ([int]($level | Select-Object -First 1) -lt 2) {
    throw "FILESTREAM file I/O access is not enabled on $SqlServer. Enable it in SQL Server Configuration Manager (FILESTREAM tab), restart the service and run this script again."
}

Write-Step 'Databases'
Invoke-SqlScript '00-Database.sql' @("DataPath=$DataPath", "LogPath=$LogPath", "FileStreamPath=$FileStreamPath")

Write-Step 'Schema'
$exists = & $sqlcmd.Source -S $SqlServer -E -h -1 -W -d ControlTower -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'ct'"
if ([int]($exists | Select-Object -First 1) -gt 0) {
    Write-Host '  Schema ct already exists, 01-Schema.sql skipped.'
} else {
    Invoke-SqlScript '01-Schema.sql' @()
}

Write-Step 'Logins and permissions'
$vars = @()
foreach ($key in $Passwords.Keys) { $vars += "$($key)Password=$(ConvertTo-PlainText $Passwords[$key])" }
Invoke-SqlScript '02-Logins.sql' $vars

if ($Seed) {
    Write-Step 'Sample data'
    if (-not $LabToolsPath) { throw '-LabToolsPath is required with -Seed.' }
    # The seed writes master data, so it runs with Windows authentication of the installing administrator.
    $connection = "Server=$SqlServer;Database=ControlTower;Integrated Security=True;TrustServerCertificate=True;Application Name=ControlTower.LabTools"
    & dotnet (Join-Path $LabToolsPath 'ControlTower.LabTools.dll') seed --connection $connection --provider SqlServer
    if ($LASTEXITCODE -ne 0) { throw 'Seed failed.' }
    Invoke-SqlScript '10-ReferenceData.sql' @()
}

Write-Host ''
Write-Host "Database ready on $SqlServer."
