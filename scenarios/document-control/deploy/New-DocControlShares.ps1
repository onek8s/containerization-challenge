<#
.SYNOPSIS
    Creates the Document Control shares on the file server: DocVault$ (revision files), Transmittals$
    (issued packages) and DocDrop$ (drop folders, one per project). Run as administrator on files01.
.EXAMPLE
    .\New-DocControlShares.ps1 -Root D:\Shares\DocControl -Domain CORP -Projects P1042,P1057
#>
param(
    [Parameter(Mandatory)] [string]$Root,
    [string]$Domain = 'CORP',
    [string[]]$Projects = @(),
    [string]$WebAccount = "$Domain\svc-dc-web",
    [string]$PackagerAccount = "$Domain\svc-dc-packager",
    [string]$IndexerAccount = "$Domain\svc-dc-indexer",
    [string]$EngineersGroup = "$Domain\DocControl-Engineers",
    [string]$ControllersGroup = "$Domain\DocControl-Controllers"
)

$ErrorActionPreference = 'Stop'

function New-Folder([string]$Path, [string[]]$Grants) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    icacls $Path /inheritance:r /grant:r 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null
    foreach ($g in $Grants) { icacls $Path /grant $g | Out-Null }
}

function New-Share([string]$Name, [string]$Path, [string]$Description) {
    if (-not (Get-SmbShare -Name $Name -ErrorAction SilentlyContinue)) {
        New-SmbShare -Name $Name -Path $Path -Description $Description -FullAccess 'BUILTIN\Administrators' -ChangeAccess 'Authenticated Users' -FolderEnumerationMode AccessBased | Out-Null
    }
    Write-Host "\\$env:COMPUTERNAME\$Name -> $Path"
}

# Vault: written by the portal and the indexer, read by the packager. Users never write here directly.
$vault = Join-Path $Root 'Vault'
New-Folder $vault @("${WebAccount}:(OI)(CI)M", "${IndexerAccount}:(OI)(CI)M", "${PackagerAccount}:(OI)(CI)RX", "${ControllersGroup}:(OI)(CI)RX")
New-Share 'DocVault$' $vault 'Document Control vault'

# Issued packages
$transmittals = Join-Path $Root 'Transmittals'
New-Folder $transmittals @("${PackagerAccount}:(OI)(CI)M", "${WebAccount}:(OI)(CI)RX", "${ControllersGroup}:(OI)(CI)RX")
New-Share 'Transmittals$' $transmittals 'Document Control issued transmittal packages'

# Drop folders
$drop = Join-Path $Root 'Drop'
New-Folder $drop @("${IndexerAccount}:(OI)(CI)M", "${EngineersGroup}:RX", "${ControllersGroup}:RX")
New-Share 'DocDrop$' $drop 'Document Control drop folders'

foreach ($project in $Projects) {
    $folder = Join-Path $drop $project
    New-Item -ItemType Directory -Force -Path $folder, (Join-Path $folder '_processed'), (Join-Path $folder '_rejected') | Out-Null
    icacls $folder /grant "${EngineersGroup}:(OI)(CI)M" "${ControllersGroup}:(OI)(CI)M" | Out-Null
    Write-Host "  drop folder $project"
}
