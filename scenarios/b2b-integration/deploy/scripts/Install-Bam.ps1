<#
    BAM activities and views (bam\*.xml) with bm.exe, read access to the views for the runtime and the
    gateway, the online window, then the reporting views in BAMPrimaryImport (Install-Database.ps1 -Reporting).
    Activity changes on an existing activity need bm.exe update-all; removing an item loses its data.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$bm = Join-Path (Get-B2BBizTalkInstallPath) 'Tracking\bm.exe'
$bamFolder = Join-Path (Get-B2BDeployRoot) '..\bam'

function Invoke-Bm([string[]] $arguments) {
    Write-B2BLog ('bm.exe {0}' -f ($arguments -join ' '))
    $output = & $bm @arguments 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { if ("$line".Trim()) { Write-B2BLog ([string]$line) } }
    return $code
}

$existing = (& $bm get-activities 2>$null) -join "`n"
foreach ($definition in 'OrderLifecycle', 'InterchangeTracking') {
    $file = Join-Path $bamFolder "$definition.xml"
    $verb = if ($existing -match "\b$definition\b") { 'update-all' } else { 'deploy-all' }
    if ((Invoke-Bm @($verb, "-DefinitionFile:$file")) -ne 0) { throw "bm.exe $verb failed for $definition." }
    if ((Invoke-Bm @('set-activitywindow', "-Activity:$definition", "-TimeLength:$($Settings.BamOnlineWindowMonths)", '-TimeUnit:Month')) -ne 0) {
        throw "bm.exe set-activitywindow failed for $definition."
    }
}

foreach ($view in 'OrderLifecycleView', 'InterchangeTrackingView') {
    foreach ($account in $Settings.GatewayAppPoolUser, $Settings.MonitorServiceUser, $Settings.OperatorsGroup) {
        Invoke-Bm @('add-account', "-AccountName:$account", "-View:$view") | Out-Null
    }
}

& (Join-Path $PSScriptRoot 'Install-Database.ps1') -Environment $Environment -Settings $Settings -Secrets $Secrets -Reporting
