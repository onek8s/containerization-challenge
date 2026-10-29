<#
.SYNOPSIS
    Installs the Credit Desk credit service on credit01.

.PARAMETER SourcePath
    Build output of src\CreditDesk.CreditService (bin\Release\net48).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $InstallPath = 'C:\Program Files\Fabrikam\Credit Desk\Credit Service',
    [string] $ServiceAccount = 'FABRIKAM\svc-creditdesk-app',
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [string] $HostName = 'credit01.fabrikam.local',
    [int] $Port = 8524,
    [string] $SqlServer = 'sql01.fabrikam.local',
    [string] $LetterQueue = 'FormatName:DIRECT=OS:docs01.fabrikam.local\private$\creditdesk.letters'
)

$ErrorActionPreference = 'Stop'
$serviceName = 'CreditDeskCredit'

Write-Host 'Installing Windows features'
Install-WindowsFeature NET-Framework-45-Core, NET-WCF-TCP-PortSharing45, MSMQ-Services, MSMQ-Server | Out-Null

# Sending to the remote transactional queue inside the database transaction needs network DTC.
Write-Host 'Configuring MSDTC for network transactions'
Set-DtcNetworkSetting -DtcName Local -AuthenticationLevel Mutual -InboundTransactionsEnabled $true `
    -OutboundTransactionsEnabled $true -RemoteClientAccessEnabled $false -RemoteAdministrationAccessEnabled $false `
    -XATransactionsEnabled $false -LUTransactionsEnabled $false -Confirm:$false
Set-Service MSDTC -StartupType Automatic
Restart-Service MSDTC
Enable-NetFirewallRule -DisplayGroup 'Distributed Transaction Coordinator'

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

Write-Host "Copying files to $InstallPath"
New-Item $InstallPath -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $SourcePath '*') $InstallPath -Recurse -Force

$configPath = Join-Path $InstallPath 'CreditDesk.CreditService.exe.config'
[xml] $config = Get-Content $configPath
$config.configuration.connectionStrings.add.connectionString =
    "Data Source=$SqlServer;Initial Catalog=CreditDesk;Integrated Security=SSPI;Application Name=Credit Desk Service;Encrypt=True;TrustServerCertificate=False"
($config.configuration.appSettings.add | Where-Object key -eq 'LetterQueue').value = $LetterQueue
$service = $config.configuration.'system.serviceModel'.services.service
$service.host.baseAddresses.add.baseAddress = "net.tcp://${HostName}:$Port/CreditDesk/"
$service.endpoint[0].identity.servicePrincipalName.value = "CreditDesk/$HostName"
$config.Save($configPath)

if (-not [Diagnostics.EventLog]::SourceExists('Credit Desk Service')) {
    New-EventLog -LogName Application -Source 'Credit Desk Service'
}

# Port sharing service must accept the service account's SID. An explicit allowAccounts list
# replaces the built-in defaults, so those are written as well.
$sid = (New-Object Security.Principal.NTAccount $ServiceAccount).Translate([Security.Principal.SecurityIdentifier]).Value
$smsvcPath = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'SMSvcHost.exe.config'
[xml] $smsvc = Get-Content $smsvcPath
$activation = $smsvc.configuration.SelectSingleNode('system.serviceModel.activation')
if (-not $activation) {
    $activation = $smsvc.configuration.AppendChild($smsvc.CreateElement('system.serviceModel.activation'))
}
$netTcp = $activation.SelectSingleNode('net.tcp')
if (-not $netTcp) { $netTcp = $activation.AppendChild($smsvc.CreateElement('net.tcp')) }
$allow = $netTcp.SelectSingleNode('allowAccounts')
if (-not $allow) {
    $allow = $netTcp.AppendChild($smsvc.CreateElement('allowAccounts'))
}
foreach ($account in 'S-1-5-18', 'S-1-5-19', 'S-1-5-20', 'S-1-5-32-544', 'S-1-5-32-568', $sid) {
    if (-not $allow.SelectSingleNode("add[@securityIdentifier='$account']")) {
        $add = $smsvc.CreateElement('add')
        $add.SetAttribute('securityIdentifier', $account)
        $allow.AppendChild($add) | Out-Null
    }
}
$smsvc.Save($smsvcPath)
Set-Service NetTcpPortSharing -StartupType Automatic
Restart-Service NetTcpPortSharing -Force

$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ServicePassword))
$exe = Join-Path $InstallPath 'CreditDesk.CreditService.exe'
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    & sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= 'Credit Desk Credit Service' | Out-Host
} else {
    & sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $plain | Out-Host
}
& sc.exe description $serviceName 'Credit limit requests and approvals for the Credit Desk intranet site.' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
& sc.exe sidtype $serviceName unrestricted | Out-Null

# "Impersonate a client after authentication" is required for per-user SQL access.
$tmp = Join-Path $env:TEMP 'creditdesk-secpol'
& secedit.exe /export /cfg "$tmp.inf" /areas USER_RIGHTS | Out-Null
$sid = (New-Object Security.Principal.NTAccount $ServiceAccount).Translate([Security.Principal.SecurityIdentifier]).Value
$lines = Get-Content "$tmp.inf" -Encoding Unicode
$lines = $lines | ForEach-Object {
    if ($_ -match '^SeImpersonatePrivilege' -and $_ -notmatch $sid) { "$_,*$sid" }
    elseif ($_ -match '^SeServiceLogonRight' -and $_ -notmatch $sid) { "$_,*$sid" }
    else { $_ }
}
$lines | Set-Content "$tmp.inf" -Encoding Unicode
& secedit.exe /configure /db "$tmp.sdb" /cfg "$tmp.inf" /areas USER_RIGHTS /quiet | Out-Null
Remove-Item "$tmp.*"

if (-not (Get-NetFirewallRule -Name CreditDeskCredit -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name CreditDeskCredit -DisplayName 'Credit Desk Credit Service (TCP-In)' `
        -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Domain | Out-Null
}

Start-Service $serviceName
Get-Service $serviceName
