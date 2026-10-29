<#
.SYNOPSIS
    Creates or upgrades the TradeRebates database on the SQL Server host.
.DESCRIPTION
    Run on the database server (the CLR assemblies are loaded from a local folder). Requires sqlcmd and SQL Server 2022
    with CLR enabled. SQL Server Agent jobs need an edition with SQL Server Agent; on editions without it use
    install-scheduled-tasks.ps1. The price book linked server needs the Microsoft Access Database Engine 2016 (x64).
.EXAMPLE
    .\install-database.ps1 -SqlInstance rebates-sql.corp.local -ErpPostingMode Live `
        -PortalAccount 'CORP\svc-rebates-web$' -ApiAccount 'CORP\svc-rebates-api$' -ActivatorAccount 'CORP\svc-rebates-ea$' `
        -FilesAccount 'CORP\svc-rebates-files'
.EXAMPLE
    # Lab server, local accounts, no finance system
    .\install-database.ps1 -SqlInstance 'host01\SQLEXPRESS' -ErpPostingMode Offline -PortalAccount 'IIS APPPOOL\rebates' `
        -ApiAccount 'IIS APPPOOL\rebates-api' -ActivatorAccount 'HOST01\svc-rebates-ea' -AnalystGroup 'HOST01\TR-Analysts' `
        -FilesRoot '\\rebates-files.corp.local\Rebates$'
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'rebates-sql.corp.local',
    [string] $DatabaseName = 'TradeRebates',
    [string] $DataPath,
    [string] $LogPath,
    [string] $ClrPath = 'D:\TradeRebates\Clr',
    [string] $ClrSource = (Join-Path $PSScriptRoot '..\src'),
    [string] $FilesRoot = '\\rebates-files.corp.local\Rebates$',
    [string] $ArchiveRoot = '\\rebates-files.corp.local\RebatesArchive$',
    [string] $PriceBookPath = '\\rebates-files.corp.local\Rebates$\PriceBook\PriceBook.accdb',
    [string] $ErpEndpoint = 'https://erp-ap.corp.local/ApService.svc',
    [ValidateSet('Live', 'Offline')] [string] $ErpPostingMode = 'Live',
    [Parameter(Mandatory)] [string] $PortalAccount,
    [Parameter(Mandatory)] [string] $ApiAccount,
    [Parameter(Mandatory)] [string] $ActivatorAccount,
    [string] $AnalystGroup = 'CORP\TR-Analysts',
    [string] $FilesAccount,
    [securestring] $FilesPassword,
    [string] $SsisPath = 'D:\TradeRebates\Ssis',
    [string] $JobOwner = 'sa',
    [switch] $SkipReferenceData,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$db = Join-Path $PSScriptRoot '..\db'

function Invoke-Sql([string] $File, [hashtable] $Vars) {
    $sqlArgs = @('-S', $SqlInstance, '-E', '-b', '-I', '-C', '-i', $File)
    # Values are quoted for sqlcmd, which rejects unquoted values with spaces (default data paths).
    foreach ($k in $Vars.Keys) { $sqlArgs += @('-v', "$k=`"$($Vars[$k])`"") }
    Write-Host "== $(Split-Path $File -Leaf)"
    & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed on $File" }
}

function Get-SqlValue([string] $Query) {
    $v = & sqlcmd.exe -S $SqlInstance -E -C -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $Query" }
    return ($v | Where-Object { $_ -ne '' } | Select-Object -First 1).Trim()
}

# Database assemblies
if (-not $SkipBuild) {
    & dotnet build (Join-Path $ClrSource 'TradeRebates.SqlClr\TradeRebates.SqlClr.csproj') -c Release
    & dotnet build (Join-Path $ClrSource 'TradeRebates.SqlClr.Integration\TradeRebates.SqlClr.Integration.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
New-Item -ItemType Directory -Path $ClrPath -Force | Out-Null
Copy-Item (Join-Path $ClrSource 'TradeRebates.SqlClr\bin\Release\net48\TradeRebates.SqlClr.dll') $ClrPath -Force
Copy-Item (Join-Path $ClrSource 'TradeRebates.SqlClr.Integration\bin\Release\net48\TradeRebates.SqlClr.Integration.dll') $ClrPath -Force
$version = (Get-Item (Join-Path $ClrPath 'TradeRebates.SqlClr.dll')).VersionInfo.FileVersion

if (-not $DataPath) { $DataPath = Get-SqlValue "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));" }
if (-not $LogPath) { $LogPath = Get-SqlValue "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400));" }
$DataPath = $DataPath.TrimEnd('\')
$LogPath = $LogPath.TrimEnd('\')

# EngineEdition 4 = Express (no SQL Server Agent)
$edition = [int](Get-SqlValue "SELECT CAST(SERVERPROPERTY('EngineEdition') AS int);")
$hasAce = [int](Get-SqlValue "SET NOCOUNT ON; DECLARE @p TABLE (n sysname, g nvarchar(255), d nvarchar(255)); INSERT @p EXEC master.dbo.sp_enum_oledb_providers; SELECT COUNT(*) FROM @p WHERE n = N'Microsoft.ACE.OLEDB.16.0';")

$vars = @{
    DatabaseName = $DatabaseName; DataPath = $DataPath; LogPath = $LogPath; ClrPath = $ClrPath; AssemblyVersion = $version
    FilesRoot = $FilesRoot; ErpEndpoint = $ErpEndpoint; ErpPostingMode = $ErpPostingMode; PriceBookPath = $PriceBookPath
    PortalAccount = $PortalAccount; ApiAccount = $ApiAccount; ActivatorAccount = $ActivatorAccount; AnalystGroup = $AnalystGroup
}

foreach ($f in '01-database.sql', '02-schemas.sql', '03-clr.sql', '04-tables.sql', '05-settings.sql') {
    Invoke-Sql (Join-Path $db $f) $vars
}

if ($hasAce -gt 0) {
    Invoke-Sql (Join-Path $db '06-linked-servers.sql') $vars
} else {
    Write-Warning 'Microsoft.ACE.OLEDB.16.0 is not installed: price book checks are turned off and workbooks cannot be imported.'
}

foreach ($f in '07-service-broker.sql', '08-claims.sql', '09-accruals-statements.sql', '10-portal-api.sql', '11-security.sql') {
    Invoke-Sql (Join-Path $db $f) $vars
}

if ($hasAce -eq 0) {
    & sqlcmd.exe -S $SqlInstance -E -C -b -d $DatabaseName -Q "UPDATE rebate.Setting SET Value = N'Off' WHERE Name = 'PriceBookCheck';"
}

if ($edition -eq 4) {
    Write-Warning 'This SQL Server edition has no SQL Server Agent. Run install-scheduled-tasks.ps1 for the import, accrual and statement schedules.'
} else {
    if (-not $FilesAccount -or -not $FilesPassword) { throw 'FilesAccount and FilesPassword are needed for the SQL Server Agent proxy.' }
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($FilesPassword))
    $jobVars = $vars.Clone()
    $jobVars += @{ FilesAccount = $FilesAccount; FilesPassword = $plain; SsisPath = $SsisPath; ArchiveRoot = $ArchiveRoot
                   JobOwner = $JobOwner; SqlServer = $SqlInstance }
    Invoke-Sql (Join-Path $db '12-agent-jobs.sql') $jobVars
}

if (-not $SkipReferenceData) {
    Invoke-Sql (Join-Path $db '13-reference-data.sql') $vars
}

# Event log used by the database routines and the statement worker.
foreach ($source in 'TradeRebates.Database', 'TradeRebates.StatementWorker') {
    if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
        New-EventLog -LogName 'Contoso Trade Rebates' -Source $source
    }
}

Write-Host "Database $DatabaseName on $SqlInstance is ready (CLR $version)."
