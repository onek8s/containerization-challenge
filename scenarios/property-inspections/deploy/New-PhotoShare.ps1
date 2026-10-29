<#
.SYNOPSIS
    Creates the photo share Inspections$ on the file server (files01) with the Intake folder.
    The share account (or the service accounts, when no share account is used) gets Modify rights.
.EXAMPLE
    .\New-PhotoShare.ps1 -Path D:\Shares\Inspections -Accounts CORP\svc-insp-share
#>
param(
    [string]$Path = 'D:\Shares\Inspections',
    [string]$ShareName = 'Inspections$',
    [Parameter(Mandatory)] [string[]]$Accounts,
    [string[]]$IntakeWriters = @()
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $Path, (Join-Path $Path 'Intake') | Out-Null

if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $ShareName -Path $Path -FullAccess 'BUILTIN\Administrators' -ChangeAccess ($Accounts + $IntakeWriters) `
        -FolderEnumerationMode AccessBased -CachingMode None | Out-Null
}

foreach ($account in $Accounts) {
    icacls $Path /grant "${account}:(OI)(CI)M" | Out-Null
}
foreach ($account in $IntakeWriters) {
    icacls (Join-Path $Path 'Intake') /grant "${account}:(OI)(CI)W" | Out-Null
}

Write-Host "Share \\$env:COMPUTERNAME\$ShareName -> $Path"
