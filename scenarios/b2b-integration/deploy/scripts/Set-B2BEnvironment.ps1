<#
    Machine settings of a B2B server: environment variables read by the applications (log root, environment name)
    and, on DEV and LAB, the b2b$ share with the folder layout the FILE locations of the bindings expect.
    -LogRoot defaults to ProgramData\VanArsdel\B2B\logs on the system drive.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [hashtable] $Secrets,
    [string] $LogRoot = (Join-Path $env:ProgramData 'VanArsdel\B2B\logs'),
    [string] $DataRoot = (Join-Path $env:ProgramData 'VanArsdel\B2B\data')
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

[Environment]::SetEnvironmentVariable('B2B_LOG_ROOT', $LogRoot, 'Machine')
[Environment]::SetEnvironmentVariable('B2B_ENVIRONMENT', $Environment, 'Machine')
$env:B2B_LOG_ROOT = $LogRoot
foreach ($app in 'OpsPortal', 'Gateway', 'Tracking', 'SlaMonitor', 'deploy') {
    New-Item -ItemType Directory -Path (Join-Path $LogRoot $app) -Force | Out-Null
}

# app pools and services write their own logs
$acl = Get-Acl $LogRoot
foreach ($identity in $Settings.GatewayAppPoolUser, $Settings.PortalAppPoolUser, $Settings.MonitorServiceUser) {
    try {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    catch { Write-B2BLog "Cannot grant $identity on the log folder yet: $($_.Exception.Message)" -Level Warning }
}
Set-Acl -Path $LogRoot -AclObject $acl
Write-B2BLog "B2B_LOG_ROOT = $LogRoot"

if ($Environment -in 'DEV', 'LAB' -and ($env:COMPUTERNAME -ieq $Settings.FileServer -or $Settings.FileServer -eq 'localhost')) {
    $folders = @(
        'archive', 'archive\mdn', 'suspended', 'credithold', 'erp\inbound', 'erp\outbound', 'erp\processed',
        'resubmit\default', 'resubmit\customer-edi', 'resubmit\supplier-edi', 'resubmit\gateway', 'resubmit\idoc',
        'sftp\C1002\inbound', 'sftp\C1002\outbound', 'sftp\S5001\inbound', 'sftp\S5001\outbound',
        'ftp\C2002\inbound', 'ftp\C2002\outbound'
    )
    foreach ($f in $folders) { New-Item -ItemType Directory -Path (Join-Path $DataRoot $f) -Force | Out-Null }

    $share = $Settings.FileShare
    if (-not (Get-SmbShare -Name $share -ErrorAction SilentlyContinue)) {
        New-SmbShare -Name $share -Path $DataRoot -ChangeAccess $Settings.HostUsersGroup, $Settings.PortalAppPoolUser -FullAccess 'BUILTIN\Administrators' | Out-Null
        Write-B2BLog "Share \\$env:COMPUTERNAME\$share -> $DataRoot"
    }
}
