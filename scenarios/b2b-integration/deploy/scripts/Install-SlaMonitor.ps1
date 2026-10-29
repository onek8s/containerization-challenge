<#
    SLA monitor Windows service on the first BizTalk server: binaries under ProgramFiles\VanArsdel\SlaMonitor,
    App.config values for the environment, InstallUtil registration, service account and recovery options.
    The service account needs "Log on as a service" (group policy B2B Servers).
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$serviceName = 'VanArsdelB2BSlaMonitor'
$build = Join-Path (Get-B2BDeployRoot) "..\src\VanArsdel.Integration.SlaMonitor\bin\$Configuration\net472"
$target = Join-Path $env:ProgramFiles 'VanArsdel\SlaMonitor'
$installUtil = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'InstallUtil.exe'
$exe = Join-Path $target 'VanArsdel.Integration.SlaMonitor.exe'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Stop-Service -Name $serviceName -Force
    (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
robocopy $build $target /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the SLA monitor failed (robocopy $LASTEXITCODE)." }

$configPath = "$exe.config"
$config = [xml](Get-Content -Path $configPath -Raw)
$values = @{
    'Monitor.Environment'        = $Environment
    'Monitor.OpsPortalUrl'       = ('https://{0}/' -f $Settings.OpsPortalHost)
    'Monitor.FallbackRecipients' = $Settings.IntegrationSupportMail
    'BizTalk.MgmtDbServer'       = $Settings.SqlServer
    'BizTalk.MgmtDbName'         = $Settings.ManagementDatabase
}
foreach ($add in $config.configuration.appSettings.add) {
    if ($values.ContainsKey($add.key)) { $add.SetAttribute('value', $values[$add.key]) }
}
foreach ($cs in $config.configuration.connectionStrings.add) {
    $database = @{ VanArsdelIntegration = $Settings.IntegrationDatabase; BAMPrimaryImport = $Settings.BamDatabase; BizTalkMsgBoxDb = 'BizTalkMsgBoxDb' }[$cs.name]
    if ($database) {
        $cs.SetAttribute('connectionString', ('Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Application Name=B2B.SlaMonitor' -f $Settings.SqlServer, $database))
    }
}
$config.Save($configPath)

if (-not $service) {
    & $installUtil /LogToConsole=false ('/LogFile={0}' -f (Join-Path $env:B2B_LOG_ROOT 'deploy\installutil-slamonitor.log')) $exe | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'InstallUtil failed for the SLA monitor.' }
    Write-B2BLog "Service $serviceName registered"
}

$account = $Settings.MonitorServiceUser
if ($account -like 'NT SERVICE\*') {
    & sc.exe config $serviceName obj= $account | Out-Null
}
else {
    & sc.exe config $serviceName obj= $account password= (Get-B2BSecret $Secrets 'MonitorServicePassword') | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw "sc.exe config failed for $serviceName." }

# restart after 1 and 5 minutes, then every 15; reset the failure count after a day
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/300000/restart/900000 | Out-Null
& sc.exe failureflag $serviceName 1 | Out-Null

Start-Service -Name $serviceName
Write-B2BLog "Service $serviceName running as $account"
