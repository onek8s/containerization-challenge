<#
.SYNOPSIS
    Prepares axint01 for the Business Connector: checks the AX 2012 R3 client components, copies the business
    connector configuration (.axc) and imports it for the service account so that Logon() finds the AOS.
.DESCRIPTION
    The Business Connector reads its configuration from the registry of the user it runs as
    (HKCU\Software\Microsoft\Dynamics\6.0\Configuration). The service account's profile is loaded and the .axc is
    imported with axutil-free registry writes; the same .axc path is set in Ax.Configuration.
    The service account must also be an AX user (System administration > Users) with the Dealer integration role,
    and CORP\svc-ax-bcproxy must be the Business Connector proxy in AX for LogonAs to work.
.EXAMPLE
    .\Install-AxClientConfig.ps1 -AxcFile \\fs01\ax\TailspinAX-BC.axc -ServiceAccount CORP\svc-dealer-ax
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AxcFile,
    [Parameter(Mandatory)][string]$ServiceAccount,
    [string]$AosServer = 'aos01.corp.local',
    [int]$AosPort = 2712
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

Write-Step 'AX client components'
$client = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Dynamics\6.0\Setup\Components' -ErrorAction SilentlyContinue
if (-not $client) { throw 'Dynamics AX 2012 client components are not installed.' }
if (-not (Test-Path "${env:ProgramFiles}\Microsoft Dynamics AX\60\BusinessConnector\Bin\Microsoft.Dynamics.BusinessConnectorNet.dll")) {
    throw 'The .NET Business Connector component is missing; rerun AX setup and add it.'
}

Write-Step "AOS $AosServer`:$AosPort"
if (-not (Test-NetConnection $AosServer -Port $AosPort -InformationLevel Quiet)) { throw "AOS $AosServer`:$AosPort is not reachable." }

Write-Step 'Configuration file'
$target = Join-Path $InstallRoot 'Ax'
New-Item $target -ItemType Directory -Force | Out-Null
Copy-Item $AxcFile (Join-Path $target 'TailspinAX-BC.axc') -Force

Write-Step "Registry configuration for $ServiceAccount"
$sid = (New-Object Security.Principal.NTAccount($ServiceAccount)).Translate([Security.Principal.SecurityIdentifier]).Value
$profile = Get-CimInstance Win32_UserProfile | Where-Object SID -eq $sid
if (-not $profile) { throw "$ServiceAccount has no profile on this server. Log on once with the account (runas /profile) and run again." }
$hive = "HKU\$sid"
$loaded = Test-Path "Registry::$hive"
if (-not $loaded) { reg load $hive (Join-Path $profile.LocalPath 'NTUSER.DAT') | Out-Null }
try {
    $config = "Registry::$hive\Software\Microsoft\Dynamics\6.0\Configuration"
    $name = 'TailspinAX-BC'
    New-Item "$config\$name" -Force | Out-Null
    # .axc is "key,type,value" lines under a header; only the values the BC needs are imported.
    foreach ($line in Get-Content $AxcFile | Where-Object { $_ -match '^\s*\w+,\d,' }) {
        $parts = $line.Trim() -split ',', 3
        Set-ItemProperty "$config\$name" -Name $parts[0] -Value $parts[2]
    }
    Set-ItemProperty "$config\$name" -Name 'aos2' -Value "$AosServer@$AosPort"
    Set-ItemProperty $config -Name 'Current' -Value $name
    Write-Host "  configuration $name is current for $ServiceAccount"
}
finally {
    [gc]::Collect()
    if (-not $loaded) { reg unload $hive | Out-Null }
}
