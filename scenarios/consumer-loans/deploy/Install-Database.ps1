<#
.SYNOPSIS
  Creates the Consumer Loans databases and SQL logins (sqlcmd from the SQL Server command line utilities).
  The schema itself is created by lending-api and the portal on their first start (Database:Upgrade = true).
#>
param(
    [string] $Config = (Join-Path $PSScriptRoot 'environment.psd1'),
    [Parameter(Mandatory)] [string] $Secrets
)
Import-Module (Join-Path $PSScriptRoot 'Common.psm1') -Force
$cfg = Import-DeployConfig $Config $Secrets
$sqlcmd = Get-Command sqlcmd.exe -ErrorAction Stop

$vars = @(
    "LendingDb=$($cfg.Sql.LendingDatabase)", "PortalDb=$($cfg.Sql.PortalDatabase)",
    "PwdApi=$(Get-Secret $cfg 'Sql.lending_api')", "PwdOffice=$(Get-Secret $cfg 'Sql.lending_office')",
    "PwdDocs=$(Get-Secret $cfg 'Sql.lending_docs')", "PwdColl=$(Get-Secret $cfg 'Sql.lending_coll')",
    "PwdTape=$(Get-Secret $cfg 'Sql.lending_tape')", "PwdPortal=$(Get-Secret $cfg 'Sql.lending_portal')"
)
foreach ($script in Get-ChildItem (Join-Path $PSScriptRoot 'sql') -Filter '*.sql' | Sort-Object Name) {
    Write-Step $script.Name
    & $sqlcmd.Source -S $cfg.Sql.Server -E -b -I -i $script.FullName -v $vars
    if ($LASTEXITCODE -ne 0) { throw "$($script.Name) failed." }
}
