<#
.SYNOPSIS
    Installs the jobs and mail intake Windows services on permits-app01.
.DESCRIPTION
    CityPermitsJobs        notifications, records filing, renewals, expiry, GIS replica refresh, settlement.
                           Runs the Geomatix engine for the replica job: run Install-GisEngine.ps1 first.
    CityPermitsMailIntake  scanned post from \\permits-app01\Scans$ (multi-page TIFF), read with Windows OCR.
                           Needs the en-GB OCR capability: Add-WindowsCapability -Online -Name 'Language.OCR~~~en-GB~0.0.1.0'
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [string] $ServiceRoot = 'C:\Services\CityPermits',
    [string] $ScanFolder = 'D:\Scans\Permits',
    [switch] $Lab
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator
$cfg = if ($Lab) { Import-PowerShellDataFile (Join-Path $PSScriptRoot 'lab.psd1') } else { @{ GisProvider = 'Geomatix'; ArchiveProvider = 'FileTable'; RecordsMode = 'Soap'; MailMode = 'Smtp' } }

if (-not (Get-WindowsCapability -Online -Name 'Language.OCR~~~en-GB~0.0.1.0').State -eq 'Installed') {
    Add-WindowsCapability -Online -Name 'Language.OCR~~~en-GB~0.0.1.0' | Out-Null
}
New-Item -ItemType Directory -Path $ScanFolder -Force | Out-Null
if (-not (Get-SmbShare -Name 'Scans$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'Scans$' -Path $ScanFolder -ChangeAccess 'CORP\PRM-Mailroom' -FullAccess $ServiceAccount | Out-Null
}
Grant-LogonAsService -Account $ServiceAccount
$credential = New-Object Management.Automation.PSCredential($ServiceAccount, $ServicePassword)

$services = @(
    @{ Name = 'CityPermitsJobs'; Folder = 'jobs'; Exe = 'CityPermits.Jobs.exe'; Settings = @{
        Gis = @{ Provider = $cfg.GisProvider; DevelopmentDataPath = $cfg.GisDataPath }
        Archive = @{ Provider = $cfg.ArchiveProvider; FolderPath = $cfg.ArchiveFolder }
        Records = @{ Mode = $cfg.RecordsMode }; Mail = @{ Mode = $cfg.MailMode; PickupFolder = 'D:\CityPermits\Mail' } } },
    @{ Name = 'CityPermitsMailIntake'; Folder = 'intake'; Exe = 'CityPermits.MailIntake.exe'; Settings = @{
        Archive = @{ Provider = $cfg.ArchiveProvider; FolderPath = $cfg.ArchiveFolder }
        Intake = @{ ScanFolder = $ScanFolder } } }
)
foreach ($svc in $services) {
    $path = Join-Path $ServiceRoot $svc.Folder
    if (Get-Service $svc.Name -ErrorAction SilentlyContinue) { Stop-Service $svc.Name -Force }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    Copy-Item (Join-Path $PublishRoot "$($svc.Folder)\*") $path -Recurse -Force
    Write-JsonSettings -Path (Join-Path $path 'appsettings.Production.json') -Settings $svc.Settings
    if (-not (Get-Service $svc.Name -ErrorAction SilentlyContinue)) {
        New-Service -Name $svc.Name -BinaryPathName (Join-Path $path $svc.Exe) -Credential $credential -StartupType Automatic | Out-Null
        sc.exe failure $svc.Name reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
    }
    Start-Service $svc.Name
    Write-Host "$($svc.Name) started"
}
