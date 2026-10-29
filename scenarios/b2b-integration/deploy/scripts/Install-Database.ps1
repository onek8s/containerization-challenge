<#
    VanArsdelIntegration database scripts (db\0*.sql in order, then the seed) and the reporting objects in
    BAMPrimaryImport. Needs sqlcmd (SQL Server command line utilities) and sysadmin on the instance.
    003_Reporting.sql and 006_ReportingSecurity.sql run after the BAM definitions exist (Install-Bam.ps1 calls
    this script again with -Reporting).
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets,
    [switch] $Reporting
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$db = Join-Path (Get-B2BDeployRoot) 'db'
$server = $Settings.SqlServer
$database = $Settings.IntegrationDatabase

$logins = @{
    RuntimeLogin        = $Settings.HostUsersGroup
    GatewayLogin        = $Settings.GatewayAppPoolUser
    PortalLogin         = $Settings.PortalAppPoolUser
    MonitorLogin        = $Settings.MonitorServiceUser
    TrackingLogin       = $Settings.TrackingSqlLogin
    TrackingBamLogin    = $Settings.TrackingBamSqlLogin
}

if (-not $Reporting) {
    $exists = & sqlcmd.exe -S $server -E -h -1 -W -Q ("SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'{0}'" -f $database)
    if ("$exists".Trim() -eq '0') {
        Write-B2BLog "Creating database $database on $server"
        & sqlcmd.exe -S $server -E -b -Q ("CREATE DATABASE [{0}]; ALTER DATABASE [{0}] SET RECOVERY FULL; ALTER DATABASE [{0}] SET READ_COMMITTED_SNAPSHOT ON;" -f $database)
        if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE $database failed." }
    }

    Invoke-B2BSqlScript -Server $server -Database $database -File (Join-Path $db '001_Schema.sql')
    Invoke-B2BSqlScript -Server $server -Database $database -File (Join-Path $db '002_Procedures.sql')
    $security = $logins.Clone()
    $security.TrackingPassword = Get-B2BSecret $Secrets 'TrackingSqlPassword'
    $security.TrackingBamPassword = Get-B2BSecret $Secrets 'TrackingBamSqlPassword'
    Invoke-B2BSqlScript -Server $server -Database $database -File (Join-Path $db '004_Security.sql') -Variables $security
    Invoke-B2BSqlScript -Server $server -Database $database -File (Join-Path $db '005_Jobs.sql') -Variables @{
        IntegrationDatabase = $database
        JobOwner            = $Settings.SqlAgentJobOwner
        RetentionDays       = $Settings.PurgeRetentionDays
        AuditRetentionDays  = $Settings.PurgeAuditRetentionDays
    }
    Invoke-B2BSqlScript -Server $server -Database $database -File (Join-Path $db '010_Seed.sql')
}

$bamExists = & sqlcmd.exe -S $server -E -h -1 -W -d $Settings.BamDatabase -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.views WHERE name = N'bam_OrderLifecycle_AllInstances'"
if ("$bamExists".Trim() -ne '1') {
    Write-B2BLog 'BAM activities are not deployed yet; reporting views follow in the Bam step.' -Level Warning
    return
}

Invoke-B2BSqlScript -Server $server -Database $Settings.BamDatabase -File (Join-Path $db '003_Reporting.sql') -Variables @{ IntegrationDatabase = $database }
Invoke-B2BSqlScript -Server $server -Database $Settings.BamDatabase -File (Join-Path $db '006_ReportingSecurity.sql') -Variables @{
    GatewayLogin     = $logins.GatewayLogin
    MonitorLogin     = $logins.MonitorLogin
    TrackingBamLogin = $logins.TrackingBamLogin
}
