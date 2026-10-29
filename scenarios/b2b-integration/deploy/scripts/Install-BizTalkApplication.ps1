<#
    BizTalk application VanArsdel.Integration through the Deployment Framework project
    (VanArsdel.Integration.Deployment.btdfproj, target Deploy): resources, GAC, bindings, virtual directories,
    host instance restarts. Before that the expanded bindings are checked with b2bdeploy inspect; afterwards the
    partner SFTP/FTP passwords are set through ExplorerOM because they are never part of a bindings file.

    The first server of BizTalkServers deploys to the management database; the others only get the GAC and the
    virtual directories (DeployBizTalkMgmtDB=False), which needs the deployment folder on a share.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets,
    [Parameter(Mandatory)] [string] $SettingsFile,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$root = Get-B2BDeployRoot
$project = Join-Path $root 'VanArsdel.Integration.Deployment.btdfproj'
$expanded = Join-Path $root ('EnvironmentSettings\Exported\PortBindings.{0}.xml' -f $Environment)

Invoke-B2BTool @('expand', '--template', (Join-Path $root 'PortBindingsMaster.xml'), '--settings', $SettingsFile, '--out', $expanded)
Invoke-B2BTool @('inspect', '--bindings', $expanded, '--environment', $Environment)

$msbuild = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'MSBuild.exe'
function Invoke-Btdf {
    param([string] $Target, [bool] $ManagementDb)
    $arguments = @($project, "/t:$Target", "/p:Configuration=$Configuration", "/p:ENV_SETTINGS=$SettingsFile",
                   "/p:DeployBizTalkMgmtDB=$ManagementDb", '/p:SkipUndeploy=False', '/nologo', '/v:minimal',
                   ('/flp:LogFile={0};Verbosity=normal' -f (Join-Path $env:B2B_LOG_ROOT ('deploy\btdf-{0:yyyyMMdd-HHmmss}.log' -f (Get-Date)))))
    Write-B2BLog "MSBuild $Target (DeployBizTalkMgmtDB=$ManagementDb)"
    & $msbuild @arguments | ForEach-Object { Write-B2BLog ([string]$_) }
    if ($LASTEXITCODE -ne 0) { throw "BTDF $Target failed with exit code $LASTEXITCODE." }
}

$servers = Get-B2BServerList $Settings.BizTalkServers
$first = $servers[0]
if ($first -ne 'localhost' -and $first -ine $env:COMPUTERNAME) {
    throw "Run the BizTalk step on $first (first server of the group in the settings)."
}

Invoke-Btdf -Target 'Deploy' -ManagementDb $true

foreach ($server in $servers | Select-Object -Skip 1) {
    if (-not $root.StartsWith('\\')) {
        Write-B2BLog "Deploy $server from the deployment share (GAC and virtual directories): the deployment folder is not on a share." -Level Warning
        continue
    }
    Write-B2BLog "GAC and virtual directories on $server"
    Invoke-Command -ComputerName $server -ArgumentList $msbuild, $project, $Configuration, $SettingsFile -ScriptBlock {
        param($msbuild, $project, $configuration, $settingsFile)
        & $msbuild $project /t:Deploy "/p:Configuration=$configuration" "/p:ENV_SETTINGS=$settingsFile" /p:DeployBizTalkMgmtDB=False /nologo /v:minimal
        if ($LASTEXITCODE -ne 0) { throw "BTDF Deploy failed on $env:COMPUTERNAME." }
    }
}

# partner passwords: ExplorerOM, on the transport data of the locations and ports that use them
if (-not (Test-B2BTrue $Settings.UsePartnerFolders)) {
    Add-Type -Path (Join-Path (Get-B2BBizTalkInstallPath) 'Developer Tools\Microsoft.BizTalk.ExplorerOM.dll')
    $group = Get-B2BBizTalkGroup
    $catalog = New-Object Microsoft.BizTalk.ExplorerOM.BtsCatalogExplorer
    $catalog.ConnectionString = 'Server={0};Database={1};Integrated Security=SSPI' -f $group.Server, $group.Database
    $app = $catalog.Applications['VanArsdel.Integration']

    $passwords = @{
        'rl_Customer_EDI_SFTP_C1002' = 'C1002_SftpPassword'; 'sp_SFTP_C1002' = 'C1002_SftpPassword'
        'rl_Supplier_EDI_SFTP_S5001' = 'S5001_SftpPassword'; 'sp_SFTP_S5001' = 'S5001_SftpPassword'
        'rl_Customer_EDI_FTP_C2002'  = 'C2002_FtpPassword';  'sp_FTP_C2002'  = 'C2002_FtpPassword'
    }
    function Set-Password([string] $data, [string] $password) {
        $xml = [xml]$data
        $node = $xml.CustomProps.SelectSingleNode('Password')
        $node.SetAttribute('vt', '8')
        $node.InnerText = $password
        return $xml.OuterXml
    }
    foreach ($port in $app.ReceivePorts) {
        foreach ($location in $port.ReceiveLocations) {
            if ($passwords.ContainsKey($location.Name)) {
                $location.TransportTypeData = Set-Password $location.TransportTypeData (Get-B2BSecret $Secrets $passwords[$location.Name])
                Write-B2BLog "Password set on $($location.Name)"
            }
        }
    }
    foreach ($port in $app.SendPorts) {
        if ($passwords.ContainsKey($port.Name)) {
            $port.PrimaryTransport.TransportTypeData = Set-Password $port.PrimaryTransport.TransportTypeData (Get-B2BSecret $Secrets $passwords[$port.Name])
            Write-B2BLog "Password set on $($port.Name)"
        }
    }
    $catalog.SaveChanges()
}

# host instances of the application's hosts, every server
$hosts = $Settings.ReceiveHost, $Settings.SendHost, $Settings.ProcessingHost
foreach ($server in $servers) {
    $computer = if ($server -eq 'localhost') { $env:COMPUTERNAME } else { $server }
    Get-CimInstance -ComputerName $computer -Namespace 'root\MicrosoftBizTalkServer' -ClassName MSBTS_HostInstance |
        Where-Object { $_.HostName -in $hosts -and $_.HostType -eq 1 -and $_.IsDisabled -eq $false } |
        ForEach-Object {
            Invoke-CimMethod -InputObject $_ -MethodName Stop | Out-Null
            Invoke-CimMethod -InputObject $_ -MethodName Start | Out-Null
            Write-B2BLog "Restarted $($_.HostName) on $computer"
        }
}
