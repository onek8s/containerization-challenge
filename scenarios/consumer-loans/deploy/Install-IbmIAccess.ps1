<#
.SYNOPSIS
  Installs and checks the IBM i Access Client Solutions Windows Application Package on a lending
  application server (web01, web02, lending-app01): ODBC driver, IBMDA400 OLE DB provider and the
  cwbx programming component used for program calls and the LNEVTQ data queue.
.PARAMETER CheckOnly
  Only report what is installed.
#>
param(
    [string] $Config = (Join-Path $PSScriptRoot 'environment.psd1'),
    [switch] $CheckOnly
)
Import-Module (Join-Path $PSScriptRoot 'Common.psm1') -Force
$cfg = Import-DeployConfig $Config

if ($cfg.Core.Provider -ne 'IbmI') {
    Write-Host "Core provider is $($cfg.Core.Provider); IBM i Access is not needed on this environment."
    return
}

function Test-AcsComponents {
    $result = [ordered]@{}
    $result['ODBC driver (64-bit)'] = [bool](Get-OdbcDriver -Name 'IBM i Access ODBC Driver' -Platform '64-bit' -ErrorAction SilentlyContinue)
    $result['OLE DB provider IBMDA400'] = Test-Path 'Registry::HKEY_CLASSES_ROOT\IBMDA400'
    foreach ($progId in 'cwbx.AS400System', 'cwbx.Program', 'cwbx.DataQueue', 'cwbx.StringConverter', 'cwbx.PackedConverter') {
        $result["COM $progId"] = $null -ne [Type]::GetTypeFromProgID($progId)
    }
    return $result
}

$status = Test-AcsComponents
if (-not $CheckOnly -and ($status.Values -contains $false)) {
    Write-Step 'Installing IBM i Access Client Solutions Windows Application Package'
    if (-not (Test-Path $cfg.Core.AcsPackage)) { throw "Package not found: $($cfg.Core.AcsPackage)" }
    # ADDLOCAL: ODBC, OLE DB, .NET data provider and the programming toolkit (cwbx)
    $msiArgs = @('/i', "`"$($cfg.Core.AcsPackage)`"", '/qn', '/norestart', 'ADDLOCAL=ALL', "/l*v `"$env:TEMP\ibmi-access-install.log`"")
    $p = Start-Process msiexec.exe -ArgumentList $msiArgs -Wait -PassThru
    if ($p.ExitCode -notin 0, 3010) { throw "msiexec exit code $($p.ExitCode), see $env:TEMP\ibmi-access-install.log" }
    $status = Test-AcsComponents
}

$status.GetEnumerator() | ForEach-Object { '{0,-36} {1}' -f $_.Key, $(if ($_.Value) { 'OK' } else { 'MISSING' }) }

Write-Step "System $($cfg.Core.SystemName)"
# host server ports: 449 port mapper, 8471 database, 8475 remote command / program call, 8472 data queue
foreach ($port in 449, 8471, 8472, 8475) {
    $ok = Test-NetConnection -ComputerName $cfg.Core.SystemName -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue
    '{0,-36} {1}' -f "TCP $port", $(if ($ok) { 'OK' } else { 'NOT REACHABLE' })
}
if ($cfg.Core.UseKerberos) {
    # sign-on uses the Kerberos ticket of the app pool / service account (EIM maps it to LNWEB / LNCOLL)
    $spn = "krbsvr400/$($cfg.Core.SystemName)"
    $found = setspn -Q $spn 2>$null | Select-String 'Existing SPN found'
    '{0,-36} {1}' -f "SPN $spn", $(if ($found) { 'OK' } else { 'NOT FOUND' })
}
if ($status.Values -contains $false) { throw 'IBM i Access components are missing.' }
