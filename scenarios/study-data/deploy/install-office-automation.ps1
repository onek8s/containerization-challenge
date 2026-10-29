<#
.SYNOPSIS
    Prepares a server for Excel automation by the Study Data site portal and jobs service.
.DESCRIPTION
    Run on every server that calculates study workbooks: the edc web server and trials-app01.
    Prerequisite: Microsoft Excel (Microsoft 365 Apps or Office LTSC 2021/2024, 64-bit) installed and
    activated with a licence that allows server-side use, the same build that the workbooks were
    validated with (see the Validation sheet of each workbook).

    - creates the systemprofile Desktop folders that Excel needs when it runs without a user session
    - sets the identity of the "Microsoft Excel Application" DCOM server to the launching user
    - grants the service accounts local launch and activation rights on it
    - disables the Excel start screen, protected view prompts and add-ins for the service accounts' profiles
.EXAMPLE
    .\install-office-automation.ps1 -Accounts 'CORP\svc-studydata-web','CORP\svc-studydata-job$'
#>
param(
    [Parameter(Mandatory)] [string[]] $Accounts
)

$ErrorActionPreference = 'Stop'
$excelAppId = '{00020812-0000-0000-C000-000000000046}'

if (-not (Test-Path 'Registry::HKEY_CLASSES_ROOT\Excel.Application\CLSID')) {
    throw 'Microsoft Excel is not installed on this server (Excel.Application is not registered).'
}

foreach ($dir in "$env:windir\System32\config\systemprofile\Desktop", "$env:windir\SysWOW64\config\systemprofile\Desktop") {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$appIdKey = "Registry::HKEY_CLASSES_ROOT\AppID\$excelAppId"
if (-not (Test-Path $appIdKey)) {
    New-Item -Path $appIdKey -Force | Out-Null
}
# No RunAs value = "The launching user".
Remove-ItemProperty -Path $appIdKey -Name RunAs -ErrorAction SilentlyContinue

function Get-Sid([string] $account) {
    (New-Object System.Security.Principal.NTAccount($account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
}

# Launch and activation: SYSTEM, Administrators, interactive users and the service accounts (local launch + activation = 0xB).
$sddl = 'O:BAG:BAD:(A;;CCDCLCSWRP;;;SY)(A;;CCDCLCSWRP;;;BA)(A;;CCDCLCSWRP;;;IU)'
foreach ($a in $Accounts) {
    $sddl += "(A;;CCDCSW;;;$(Get-Sid $a))"
}
$helper = [wmiclass]'Win32_SecurityDescriptorHelper'
Set-ItemProperty -Path $appIdKey -Name LaunchPermission -Value ($helper.SDDLToBinarySD($sddl).BinarySD) -Type Binary

$accessSddl = 'O:BAG:BAD:(A;;CCDCLC;;;SY)(A;;CCDCLC;;;BA)(A;;CCDCLC;;;PS)'
foreach ($a in $Accounts) {
    $accessSddl += "(A;;CCDC;;;$(Get-Sid $a))"
}
Set-ItemProperty -Path $appIdKey -Name AccessPermission -Value ($helper.SDDLToBinarySD($accessSddl).BinarySD) -Type Binary

# Machine policy for unattended Excel (applies to every profile).
$policy = 'HKLM:\SOFTWARE\Policies\Microsoft\Office\16.0\Excel\Options'
New-Item -Path $policy -Force | Out-Null
Set-ItemProperty -Path $policy -Name DisableBootToOfficeStart -Value 1 -Type DWord
$security = 'HKLM:\SOFTWARE\Policies\Microsoft\Office\16.0\Excel\Security'
New-Item -Path $security -Force | Out-Null
Set-ItemProperty -Path $security -Name VBAWarnings -Value 4 -Type DWord
New-Item -Path "$security\ProtectedView" -Force | Out-Null
Set-ItemProperty -Path "$security\ProtectedView" -Name DisableInternetFilesInPV -Value 1 -Type DWord
Set-ItemProperty -Path "$security\ProtectedView" -Name DisableUnsafeLocationsInPV -Value 1 -Type DWord

New-Item -ItemType Directory -Force -Path 'C:\StudyData\Workbooks' | Out-Null
foreach ($a in $Accounts) {
    icacls 'C:\StudyData\Workbooks' /grant "$($a):(OI)(CI)M" | Out-Null
}

Write-Host 'Excel automation prepared. Sign in once as each service account (or load its profile) so Excel can complete its first-run setup, then check:'
Write-Host '  save a Vital Signs form in the site portal and check that BMI and BSA are calculated.'
