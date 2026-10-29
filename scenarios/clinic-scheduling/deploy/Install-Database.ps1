<#
.SYNOPSIS
    Creates or upgrades the SouthridgeClinic and SouthridgeWorkflow databases, the ASP.NET application services
    tables, the workflow persistence store, the SQL logins and the SQL Server Agent jobs.
.EXAMPLE
    .\Install-Database.ps1 -Environment Production -SecretsFile ..\..\southridge-secrets\production.ps1
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production',
    [string] $SecretsFile
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings = Get-DeploySettings $Environment $SecretsFile
$db = Join-Path $ScenarioRoot 'db'
$sqlAdminPassword = Get-Secret $settings 'SqlAdminPassword' 'Lab!Passw0rd2013'

Write-Step "Databases on $($settings.Sql.Server)"
Invoke-SqlFile $settings (Join-Path $db '01-create-database.sql') 'master' @("DataPath=$($settings.Sql.DataPath)", "LogPath=$($settings.Sql.LogPath)")

Write-Step 'ASP.NET membership, roles and profile (aspnet_regsql)'
$regsql = Join-Path $env:windir 'Microsoft.NET\Framework\v4.0.30319\aspnet_regsql.exe'
& $regsql -S $settings.Sql.Server -U $settings.Sql.AdminUser -P $sqlAdminPassword -d $settings.Sql.Database -A mrp -Q
if ($LASTEXITCODE -ne 0) { throw 'aspnet_regsql failed.' }

Write-Step 'Clinic schema, procedures and reference data'
foreach ($script in '02-schema.sql', '03-procedures.sql', '04-reference-data.sql', '08-elmah.sql') {
    Invoke-SqlFile $settings (Join-Path $db $script) $settings.Sql.Database
}
if ($settings.Sql.DemoData) {
    Invoke-SqlFile $settings (Join-Path $db '05-demo-data.sql') $settings.Sql.Database
}

Write-Step 'Workflow persistence store (.NET Framework 3.0 SqlPersistenceService scripts)'
$wfSql = Join-Path $env:windir 'Microsoft.NET\Framework\v3.0\Windows Workflow Foundation\SQL\EN'
foreach ($script in 'SqlPersistenceService_Schema.sql', 'SqlPersistenceService_Logic.sql') {
    Invoke-SqlFile $settings (Join-Path $wfSql $script) $settings.Sql.Workflow
}

Write-Step 'Logins and permissions'
Invoke-SqlFile $settings (Join-Path $db '06-logins.sql') 'master' @(
    "WebPassword=$(Get-Secret $settings 'WebDbPassword' 'dev-web')",
    "ServicesPassword=$(Get-Secret $settings 'ServicesDbPassword' 'dev-services')",
    "InterfacePassword=$(Get-Secret $settings 'InterfaceDbPassword' 'dev-interface')",
    "BatchPassword=$(Get-Secret $settings 'BatchDbPassword' 'dev-batch')",
    "PortalPassword=$(Get-Secret $settings 'PortalDbPassword' 'dev-portal')",
    "ReportsPassword=$(Get-Secret $settings 'ReportsDbPassword' 'dev-reports')")

Write-Step 'SQL Server Agent jobs'
Invoke-SqlFile $settings (Join-Path $db '07-agent-jobs.sql') 'msdb'
