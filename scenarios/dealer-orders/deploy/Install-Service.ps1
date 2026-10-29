<#
.SYNOPSIS
    Installs one Dealer Orders Windows service (Topshelf) with its account, configuration and logs folder.
    AxConnector and PriceSync run on axint01 and need the Dynamics AX 2012 R3 client components with the .NET Business
    Connector and the AX client configuration (Install-AxClientConfig.ps1) for the service account.
.EXAMPLE
    .\Install-Service.ps1 -Component AxConnector -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-ax -ServicePassword $p -SqlPassword $s
.EXAMPLE
    .\Install-Service.ps1 -Component EdiGateway -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-edi -ServicePassword $p -SqlPassword $s `
        -Settings @{ 'Edi.As2.CertificateThumbprint' = '8C2E...' }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('AxConnector', 'PriceSync', 'EdiGateway', 'Notifications')][string]$Component,
    [Parameter(Mandatory)][string]$PublishPath,
    [string]$ZoneName = 'corp.local',
    [Parameter(Mandatory)][string]$ServiceAccount,
    [Parameter(Mandatory)][securestring]$ServicePassword,
    [string]$SqlServer,
    [Parameter(Mandatory)][object]$SqlPassword,
    [hashtable]$Settings = @{}
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

$def = $Services[$Component]
if (-not $SqlServer) { $SqlServer = "sql01.$ZoneName" }
$source = Join-Path $PublishPath $def.Project
$target = Join-Path $InstallRoot "Services\$Component"
$exe = Join-Path $target "$($def.Project).exe"

if (-not (Test-NetFramework472)) { throw '.NET Framework 4.7.2 is required.' }
if ($env:COMPUTERNAME -ne $def.Server.ToUpperInvariant()) { Write-Warning "$Component normally runs on $($def.Server); this is $env:COMPUTERNAME." }

if ($def.NeedsAxClient) {
    Write-Step 'Dynamics AX client components'
    $bc = Get-ChildItem "$env:windir\Microsoft.NET\assembly\GAC_64\Microsoft.Dynamics.BusinessConnectorNet" -Recurse -Filter '*.dll' -ErrorAction SilentlyContinue
    if (-not $bc) { throw 'The AX 2012 .NET Business Connector is not installed (AX 2012 R3 setup, client components: .NET Business Connector).' }
    Write-Host "  $($bc[0].FullName)"
    $axc = Join-Path $InstallRoot 'Ax\TailspinAX-BC.axc'
    if (-not (Test-Path $axc)) { throw "AX client configuration $axc missing. Run Install-AxClientConfig.ps1 first." }
}

$existing = Get-Service $def.ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Step "Stop and remove the old $($def.ServiceName)"
    if ($existing.Status -ne 'Stopped') { Stop-Service $def.ServiceName -Force }
    & $exe uninstall | Out-Null
}

Write-Step "Files to $target"
if (Test-Path "$exe.config") { Copy-Item "$exe.config" "$env:TEMP\$Component.config" -Force }
robocopy $source $target /MIR /XF "$($def.Project).exe.config" /NFL /NDL /NJH /NJS | Out-Null
if (-not (Test-Path "$exe.config")) { Copy-Item (Join-Path $source "$($def.Project).exe.config") $target }

Write-Step 'Configuration'
$values = @{ 'Logging.Folder' = (Join-Path $InstallRoot 'Logs') }
if ($def.NeedsAxClient) {
    $values['Ax.Mode'] = 'BusinessConnector'
    $values['Ax.Configuration'] = Join-Path $InstallRoot 'Ax\TailspinAX-BC.axc'
}
foreach ($k in $Settings.Keys) { $values[$k] = $Settings[$k] }
Set-ConfigValues -Path "$exe.config" -AppSettings $values `
    -ConnectionString (New-SqlConnectionString -Server $SqlServer -User $def.SqlLogin -Password $SqlPassword -ApplicationName "DealerOrders.$Component")

Write-Step 'Account rights'
Grant-UserRight -Account $ServiceAccount -Right 'SeServiceLogonRight'
$logs = Join-Path $InstallRoot 'Logs'
New-Item $logs -ItemType Directory -Force | Out-Null
icacls $logs /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
icacls $target /grant "${ServiceAccount}:(OI)(CI)RX" | Out-Null
if ($Component -eq 'AxConnector') {
    # The simulator state file and the AX client trace folder are written by the service on test systems.
    New-Item (Join-Path $InstallRoot 'Ax') -ItemType Directory -Force | Out-Null
    icacls (Join-Path $InstallRoot 'Ax') /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
}
if ($def.UrlAcl) {
    netsh http delete urlacl url=$($def.UrlAcl) 2>$null | Out-Null
    netsh http add urlacl url=$($def.UrlAcl) user=$ServiceAccount | Out-Null
    New-NetFirewallRule -DisplayName 'Dealer Orders AS2' -Direction Inbound -Protocol TCP -LocalPort 4080 -Action Allow -ErrorAction SilentlyContinue | Out-Null
    Write-Host "  URL reservation $($def.UrlAcl) for $ServiceAccount"
}

Write-Step "Install $($def.ServiceName)"
& $exe install -username:$ServiceAccount -password:(ConvertTo-PlainText $ServicePassword) --delayed
if ($LASTEXITCODE -ne 0) { throw 'Service installation failed.' }
Start-Service $def.ServiceName
Get-Service $def.ServiceName | Format-Table -AutoSize
