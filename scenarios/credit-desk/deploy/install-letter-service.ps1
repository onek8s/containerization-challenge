<#
.SYNOPSIS
    Installs the Credit Desk letter service on docs01.

.DESCRIPTION
    Requirements on the server before running:
      - Microsoft Word (Office LTSC 2021 or Microsoft 365 Apps, 64-bit), activated, first-run
        dialogs completed once for the service account.
      - Printer \\print01.fabrikam.local\FIN-LETTERHEAD-01 connected for the service account.

.PARAMETER SourcePath
    Build output of src\CreditDesk.LetterService (bin\Release\net48).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $InstallPath = 'C:\Program Files\Fabrikam\Credit Desk\Letter Service',
    [string] $ServiceAccount = 'FABRIKAM\svc-creditdesk-doc',
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [string] $CreditServiceAccount = 'FABRIKAM\svc-creditdesk-app',
    [string] $TemplatePath = '\\fs01.fabrikam.local\templates$\CreditDesk\CreditLimitLetter.dotx',
    [string] $OutputFolder = '\\fs01.fabrikam.local\letters$\credit',
    [string] $Printer = '\\print01.fabrikam.local\FIN-LETTERHEAD-01',
    [string] $SqlServer = 'sql01.fabrikam.local'
)

$ErrorActionPreference = 'Stop'
$serviceName = 'CreditDeskLetters'

Write-Host 'Installing Windows features'
Install-WindowsFeature MSMQ-Server | Out-Null

if (-not (Get-ItemProperty 'HKLM:\SOFTWARE\Classes\Word.Application\CLSID' -ErrorAction SilentlyContinue)) {
    throw 'Microsoft Word is not installed on this server.'
}

Write-Host 'Configuring MSDTC'
Set-DtcNetworkSetting -DtcName Local -AuthenticationLevel Mutual -InboundTransactionsEnabled $true `
    -OutboundTransactionsEnabled $true -RemoteClientAccessEnabled $false -RemoteAdministrationAccessEnabled $false `
    -XATransactionsEnabled $false -LUTransactionsEnabled $false -Confirm:$false
Restart-Service MSDTC
Enable-NetFirewallRule -DisplayGroup 'Distributed Transaction Coordinator'
Enable-NetFirewallRule -DisplayGroup 'Message Queuing'

Write-Host 'Creating queues'
Add-Type -AssemblyName System.Messaging
foreach ($q in '.\private$\creditdesk.letters', '.\private$\creditdesk.letters.failed') {
    if (-not [System.Messaging.MessageQueue]::Exists($q)) {
        $queue = [System.Messaging.MessageQueue]::Create($q, $true)
    } else {
        $queue = New-Object System.Messaging.MessageQueue $q
    }
    $queue.Label = 'Credit Desk ' + ($q -replace '.*\\', '')
    $queue.SetPermissions($ServiceAccount, [System.Messaging.MessageQueueAccessRights]::FullControl)
    if ($q -like '*letters') {
        $queue.SetPermissions($CreditServiceAccount, [System.Messaging.MessageQueueAccessRights]::WriteMessage -bor
            [System.Messaging.MessageQueueAccessRights]::GetQueueProperties)
    }
    $queue.SetPermissions('Everyone', [System.Messaging.MessageQueueAccessRights]::GetQueueProperties)
    $queue.Dispose()
}

# Word needs a Desktop folder in the system profiles when started without an interactive session.
foreach ($folder in "$env:windir\System32\config\systemprofile\Desktop", "$env:windir\SysWOW64\config\systemprofile\Desktop") {
    New-Item $folder -ItemType Directory -Force | Out-Null
}

if (Get-Service $serviceName -ErrorAction SilentlyContinue) { Stop-Service $serviceName -Force }

Write-Host "Copying files to $InstallPath"
New-Item $InstallPath -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $SourcePath '*') $InstallPath -Recurse -Force

$configPath = Join-Path $InstallPath 'CreditDesk.LetterService.exe.config'
[xml] $config = Get-Content $configPath
$settings = $config.configuration.appSettings.add
($settings | Where-Object key -eq 'TemplatePath').value = $TemplatePath
($settings | Where-Object key -eq 'OutputFolder').value = $OutputFolder
($settings | Where-Object key -eq 'Printer').value = $Printer
$config.configuration.connectionStrings.add.connectionString =
    "Data Source=$SqlServer;Initial Catalog=CreditDesk;Integrated Security=SSPI;Application Name=Credit Desk Letters;Encrypt=True;TrustServerCertificate=False"
$config.Save($configPath)

if (-not [Diagnostics.EventLog]::SourceExists('Credit Desk Letters')) {
    New-EventLog -LogName Application -Source 'Credit Desk Letters'
}

# Word automation runs under the service account; allow it to launch Word through DCOM.
$wordAppId = '{00020906-0000-0000-C000-000000000046}'
Write-Host "Check DCOM settings for Microsoft Word 97 - 2003 Document ($wordAppId) in dcomcnfg:"
Write-Host "  Launch and Activation Permissions: allow Local Launch / Local Activation for $ServiceAccount"
Write-Host "  Identity: The launching user"

$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ServicePassword))
$exe = Join-Path $InstallPath 'CreditDesk.LetterService.exe'
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    & sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= 'Credit Desk Letter Service' depend= MSMQ/MSDTC | Out-Host
} else {
    & sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $plain depend= MSMQ/MSDTC | Out-Host
}
& sc.exe description $serviceName 'Produces and prints customer credit limit letters for Credit Desk.' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/120000/restart/300000/none/0 | Out-Null

# sc.exe does not grant "Log on as a service".
$tmp = Join-Path $env:TEMP 'creditdesk-secpol'
& secedit.exe /export /cfg "$tmp.inf" /areas USER_RIGHTS | Out-Null
$sid = (New-Object Security.Principal.NTAccount $ServiceAccount).Translate([Security.Principal.SecurityIdentifier]).Value
$lines = Get-Content "$tmp.inf" -Encoding Unicode | ForEach-Object {
    if ($_ -match '^SeServiceLogonRight' -and $_ -notmatch $sid) { "$_,*$sid" } else { $_ }
}
$lines | Set-Content "$tmp.inf" -Encoding Unicode
& secedit.exe /configure /db "$tmp.sdb" /cfg "$tmp.inf" /areas USER_RIGHTS /quiet | Out-Null
Remove-Item "$tmp.*"

Write-Host 'Sign in once as the service account, start Word, accept the license and first-run dialogs,'
Write-Host "and connect the printer $Printer. Then start the service:"
Write-Host "  Start-Service $serviceName"
