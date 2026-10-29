<#
.SYNOPSIS
    Creates the "StudyData PDF" printer used to print casebooks to PDF.
.DESCRIPTION
    Run on trials-app01 (casebook archive) and the monitor web server. Uses the Microsoft Print To PDF
    driver on a local port that is a fixed file; the applications print one job at a time and move the
    file away after each job.
.EXAMPLE
    .\install-pdf-printer.ps1 -Accounts 'CORP\svc-studydata-web','CORP\svc-studydata-job$'
#>
param(
    [string] $PrinterName = 'StudyData PDF',
    [string] $SpoolFolder = 'C:\StudyData\Spool',
    [string] $PortFile = 'C:\StudyData\Spool\studydata.pdf',
    [Parameter(Mandatory)] [string[]] $Accounts
)

$ErrorActionPreference = 'Stop'

$feature = Get-WindowsOptionalFeature -Online -FeatureName Printing-PrintToPDFServices-Features -ErrorAction SilentlyContinue
if ($feature -and $feature.State -ne 'Enabled') {
    Enable-WindowsOptionalFeature -Online -FeatureName Printing-PrintToPDFServices-Features -NoRestart | Out-Null
}
if (-not (Get-PrinterDriver -Name 'Microsoft Print To PDF' -ErrorAction SilentlyContinue)) {
    throw 'The Microsoft Print To PDF driver is not available on this server.'
}

New-Item -ItemType Directory -Force -Path $SpoolFolder | Out-Null
foreach ($a in $Accounts) {
    icacls $SpoolFolder /grant "$($a):(OI)(CI)M" | Out-Null
}

if (-not (Get-PrinterPort -Name $PortFile -ErrorAction SilentlyContinue)) {
    Add-PrinterPort -Name $PortFile
}
if (-not (Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue)) {
    Add-Printer -Name $PrinterName -DriverName 'Microsoft Print To PDF' -PortName $PortFile
}
Set-PrintConfiguration -PrinterName $PrinterName -PaperSize A4

# The service accounts print without a user session: give them print and manage-documents rights.
$printer = Get-Printer -Name $PrinterName -Full
$sddl = $printer.PermissionSDDL
foreach ($a in $Accounts) {
    $sid = (New-Object System.Security.Principal.NTAccount($a)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    if ($sddl -notmatch $sid) {
        $sddl += "(A;;LCSWSDRCWDWO;;;$sid)(A;OIIO;RPWPSDRCWDWO;;;$sid)"
    }
}
Set-Printer -Name $PrinterName -PermissionSDDL $sddl
Write-Host "Printer '$PrinterName' prints to $PortFile"
