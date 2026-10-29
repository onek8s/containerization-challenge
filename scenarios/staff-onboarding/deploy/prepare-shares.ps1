<#
.SYNOPSIS
    Creates the onboarding share and DFS folders and copies configuration and mail templates.
.DESCRIPTION
    Run on the file server files01 as an administrator. The DFS part needs the DFSN module and
    a domain based namespace \\corp.local\dfs. Home and archive targets can be on other servers.
.EXAMPLE
    .\prepare-shares.ps1 -SourceRoot C:\Install\StaffOnboarding\deploy -HomeTarget \\fs02.corp.local\home$ -ArchiveTarget \\fs03.corp.local\archive$
#>
param(
    [string] $SharePath = 'D:\Shares\Onboarding',
    [string] $ShareName = 'Onboarding$',
    [Parameter(Mandatory)] [string] $SourceRoot,
    [string] $Domain = 'CORP',
    [string] $Namespace = '\\corp.local\dfs',
    [Parameter(Mandatory)] [string] $HomeTarget,
    [Parameter(Mandatory)] [string] $ArchiveTarget
)

$ErrorActionPreference = 'Stop'

$web = "$Domain\svc-onbweb$"
$flow = "$Domain\svc-onbflow$"
$idm = "$Domain\svc-idm$"
$hr = "$Domain\APP-Onboarding-HR"
$it = "$Domain\APP-Onboarding-IT"

foreach ($f in 'Config', 'Templates', 'Packs', 'Documents') {
    New-Item -ItemType Directory -Force -Path (Join-Path $SharePath $f) | Out-Null
}

Copy-Item (Join-Path $SourceRoot 'config\access-map.xml') (Join-Path $SharePath 'Config') -Force
Copy-Item (Join-Path $SourceRoot 'config\holidays.txt') (Join-Path $SharePath 'Config') -Force
Copy-Item (Join-Path $SourceRoot 'templates\*.txt') (Join-Path $SharePath 'Templates') -Force

icacls $SharePath /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
icacls (Join-Path $SharePath 'Config') /grant:r "${web}:(OI)(CI)RX" "${flow}:(OI)(CI)RX" "${it}:(OI)(CI)M" | Out-Null
icacls (Join-Path $SharePath 'Templates') /grant:r "${flow}:(OI)(CI)RX" "${hr}:(OI)(CI)M" | Out-Null
icacls (Join-Path $SharePath 'Packs') /grant:r "${flow}:(OI)(CI)M" "${hr}:(OI)(CI)RX" | Out-Null
icacls (Join-Path $SharePath 'Documents') /grant:r "${web}:(OI)(CI)M" "${hr}:(OI)(CI)RX" | Out-Null
icacls $SharePath /grant "${web}:RX" "${flow}:RX" "${hr}:RX" "${it}:RX" | Out-Null

if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $ShareName -Path $SharePath -FullAccess 'Authenticated Users' -FolderEnumerationMode AccessBased -EncryptData $true | Out-Null
}
Set-SmbServerConfiguration -RequireSecuritySignature $true -EnableSecuritySignature $true -Force

# DFS folders for home folders and the archive
Import-Module DFSN
foreach ($p in @(@{ Path = "$Namespace\home"; Target = $HomeTarget }, @{ Path = "$Namespace\archive\home"; Target = $ArchiveTarget })) {
    if (-not (Get-DfsnFolder -Path $p.Path -ErrorAction SilentlyContinue)) {
        New-DfsnFolder -Path $p.Path -TargetPath $p.Target -EnableInsiteReferrals $true | Out-Null
        Write-Host "DFS folder $($p.Path) -> $($p.Target)"
    }
}

Write-Host @"
Shares prepared. On the file servers behind $HomeTarget and $ArchiveTarget also:
  - grant $idm Full Control on the share root folders (this folder only and subfolders)
  - give $idm the 'Restore files and directories' right (SeRestorePrivilege) so it can make users owners of their home folders
  - require SMB signing (Set-SmbServerConfiguration -RequireSecuritySignature `$true)
"@
