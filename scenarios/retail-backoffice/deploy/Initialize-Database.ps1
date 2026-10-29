#Requires -Version 7.2
<#
.SYNOPSIS
  Creates the databases and logins and runs the schema scripts in ..\db with sqlcmd.
.DESCRIPTION
  Runs as a sysadmin (Windows authentication of the deployment account). The SQL login passwords
  come from the vault ("sql-<login>"). The scripts are re-runnable.
#>
param(
    [Parameter(Mandatory)][string] $Environment,
    [string] $SqlCmd = 'sqlcmd'
)
. "$PSScriptRoot\Common.ps1"
$envData = Import-RetailEnvironment $Environment
$db = Join-Path $PSScriptRoot '..\db'

$logins = [ordered]@{
    Catalog = 'svc_catalog'; Pricing = 'svc_pricing'; Inventory = 'svc_inventory'; Allocation = 'svc_allocation'
    Orders = 'svc_orders'; Returns = 'svc_returns'; StoreOps = 'svc_storeops'; ErpConnector = 'svc_erpconnector'
    Notifications = 'svc_notifications'; Reporting = 'svc_reporting'; Scheduler = 'svc_scheduler'
}

$vars = @('-v', "Environment=$($envData.Name)")
foreach ($entry in $logins.GetEnumerator()) {
    $vars += "Pwd$($entry.Key)=$(Get-RetailSecret $envData "sql-$($entry.Value)")"
}

# -I: QUOTED_IDENTIFIER ON, needed by the filtered unique indexes. -b: stop on the first error.
& $SqlCmd -S $envData.SqlServer -E -C -I -b -i (Join-Path $db '00-databases.sql') @vars
if ($LASTEXITCODE -ne 0) { throw '00-databases.sql failed.' }

$scripts = [ordered]@{
    '10-catalog.sql' = 'VanArsdelCatalog'; '15-pricing.sql' = 'VanArsdelPricing'; '20-inventory.sql' = 'VanArsdelInventory'
    '30-allocation.sql' = 'VanArsdelAllocation'; '40-orders.sql' = 'VanArsdelOrders'; '50-returns.sql' = 'VanArsdelReturns'
    '60-storeops.sql' = 'VanArsdelStoreOps'; '70-erpconnector.sql' = 'VanArsdelErpConnector'
    '80-notifications.sql' = 'VanArsdelNotifications'; '85-reporting.sql' = 'VanArsdelReporting'; '90-scheduler-quartz.sql' = 'VanArsdelScheduler'
}
foreach ($s in $scripts.GetEnumerator()) {
    $database = $s.Value
    # The EF scripts are not re-runnable: skip a database that already has tables.
    $tables = & $SqlCmd -S $envData.SqlServer -E -C -d $database -h -1 -W -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0'
    if ([int]($tables | Select-Object -First 1) -gt 0 -and $s.Key -notin '15-pricing.sql', '85-reporting.sql', '90-scheduler-quartz.sql') {
        Write-Host "$database has tables, $($s.Key) skipped (apply changes with a migration script)." -ForegroundColor Yellow
        continue
    }
    Write-Host "$($s.Key) -> $database" -ForegroundColor Cyan
    & $SqlCmd -S $envData.SqlServer -E -C -I -b -d $database -i (Join-Path $db $s.Key)
    if ($LASTEXITCODE -ne 0) { throw "$($s.Key) failed." }
}
