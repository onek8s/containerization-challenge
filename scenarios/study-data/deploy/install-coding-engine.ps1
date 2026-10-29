<#
.SYNOPSIS
    Registers the Coderis AutoCode 6 engine for use by the monitor site and the jobs service.
.DESCRIPTION
    Prerequisites (from Coderis, not in this repository):
      - Coderis AutoCode 6.4 server setup (AutoCode6Setup.exe), installs the 32-bit engine CoderisAC6.dll
        under C:\Program Files (x86)\Coderis\AutoCode 6
      - licence file for this server (studydata.lic, issued for the host name and MAC address)
      - MedDRA and WHODrug dictionary files imported with the Coderis Dictionary Manager
    The engine is 32-bit and the Study Data applications are 64-bit, so the engine runs in the COM
    surrogate (dllhost.exe): this script adds the AppID with an empty DllSurrogate value and grants the
    service accounts launch and access rights.
.EXAMPLE
    .\install-coding-engine.ps1 -Accounts 'CORP\svc-studydata-web','CORP\svc-studydata-job$'
#>
param(
    [Parameter(Mandatory)] [string[]] $Accounts,
    [string] $InstallFolder = 'C:\Program Files (x86)\Coderis\AutoCode 6'
)

$ErrorActionPreference = 'Stop'
$clsid = '{8B1F3C27-5D4E-4A61-9E0B-2C7D41A9F6E3}'

$clsKey = "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$clsid"
if (-not (Test-Path "$clsKey\InprocServer32")) {
    throw "Coderis AutoCode 6 is not registered. Run the Coderis setup first (regsvr32 from $InstallFolder\CoderisAC6.dll)."
}
if (-not (Test-Path (Join-Path $InstallFolder 'licence\studydata.lic'))) {
    Write-Warning "Licence file $InstallFolder\licence\studydata.lic is missing; the engine refuses to open dictionaries without it."
}

# AppID with DllSurrogate = "" (default surrogate) in both registry views.
foreach ($root in 'HKLM:\SOFTWARE\Classes', 'HKLM:\SOFTWARE\Classes\WOW6432Node') {
    $appId = "$root\AppID\$clsid"
    New-Item -Path $appId -Force | Out-Null
    Set-ItemProperty -Path $appId -Name '(default)' -Value 'Coderis AutoCode 6 (surrogate)'
    Set-ItemProperty -Path $appId -Name DllSurrogate -Value ''
}
Set-ItemProperty -Path $clsKey -Name AppID -Value $clsid
New-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$clsid" -Force | Out-Null
Set-ItemProperty -Path "HKLM:\SOFTWARE\Classes\CLSID\$clsid" -Name AppID -Value $clsid

$helper = [wmiclass]'Win32_SecurityDescriptorHelper'
$launch = 'O:BAG:BAD:(A;;CCDCLCSWRP;;;SY)(A;;CCDCLCSWRP;;;BA)'
$access = 'O:BAG:BAD:(A;;CCDCLC;;;SY)(A;;CCDCLC;;;BA)'
foreach ($a in $Accounts) {
    $sid = (New-Object System.Security.Principal.NTAccount($a)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $launch += "(A;;CCDCSW;;;$sid)"
    $access += "(A;;CCDC;;;$sid)"
}
foreach ($root in 'HKLM:\SOFTWARE\Classes', 'HKLM:\SOFTWARE\Classes\WOW6432Node') {
    Set-ItemProperty -Path "$root\AppID\$clsid" -Name LaunchPermission -Value ($helper.SDDLToBinarySD($launch).BinarySD) -Type Binary
    Set-ItemProperty -Path "$root\AppID\$clsid" -Name AccessPermission -Value ($helper.SDDLToBinarySD($access).BinarySD) -Type Binary
}

foreach ($a in $Accounts) {
    icacls (Join-Path $InstallFolder 'licence') /grant "$($a):(OI)(CI)RX" | Out-Null
    icacls (Join-Path $InstallFolder 'dictionaries') /grant "$($a):(OI)(CI)RX" | Out-Null
}
Write-Host 'Coderis AutoCode 6 registered for out-of-process use.'
