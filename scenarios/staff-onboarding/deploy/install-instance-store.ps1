<#
.SYNOPSIS
    Creates the workflow instance store schema in the StaffOnboardingInstances database.
.DESCRIPTION
    Uses the SQL scripts installed with .NET Framework 4.x. Run on a Windows machine with .NET Framework 4.8
    and the SqlServer PowerShell module. The database must exist (sql\01-create-databases.sql).
#>
param(
    [string] $SqlServer = 'sql01.corp.local',
    [string] $Database = 'StaffOnboardingInstances'
)

$ErrorActionPreference = 'Stop'
Import-Module SqlServer

$scripts = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\SQL\en'
foreach ($file in 'SqlWorkflowInstanceStoreSchema.sql', 'SqlWorkflowInstanceStoreLogic.sql') {
    $path = Join-Path $scripts $file
    if (-not (Test-Path $path)) { throw "$path not found. Install .NET Framework 4.8 on this machine." }
    Write-Host "Running $file on $SqlServer/$Database"
    Invoke-Sqlcmd -ServerInstance $SqlServer -Database $Database -InputFile $path -TrustServerCertificate
}

# Long running timers are picked up by every host that polls the store; keep the lock owner table small.
Invoke-Sqlcmd -ServerInstance $SqlServer -Database $Database -TrustServerCertificate -Query @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InstancesTable_PendingTimer')
    CREATE INDEX IX_InstancesTable_PendingTimer ON [System.Activities.DurableInstancing].[InstancesTable] (PendingTimer) WHERE PendingTimer IS NOT NULL;
"@
Write-Host 'Instance store ready.'
