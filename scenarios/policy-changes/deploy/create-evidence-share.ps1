<#
.SYNOPSIS
    Creates the share for PolicyDesk screenshots on the file server.
.DESCRIPTION
    Desk stations write, the portal reads. Screenshots show customer data; nobody else gets access.
.EXAMPLE
    .\create-evidence-share.ps1 -Path D:\Shares\PolicyChanges -WebAccount 'CORP\svc-policychanges' -DeskGroup 'CORP\Policy Desk Stations'
#>
param(
    [string] $Path = 'D:\Shares\PolicyChanges',
    [string] $ShareName = 'PolicyChanges$',
    [Parameter(Mandatory)] [string] $WebAccount,
    [Parameter(Mandatory)] [string] $DeskGroup
)

$ErrorActionPreference = 'Stop'

$evidence = Join-Path $Path 'Evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

icacls $Path /inheritance:r | Out-Null
icacls $Path /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null
icacls $evidence /grant "$($DeskGroup):(OI)(CI)M" "$($WebAccount):(OI)(CI)RX" | Out-Null

if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $ShareName -Path $Path -ChangeAccess $DeskGroup -ReadAccess $WebAccount -FolderEnumerationMode AccessBased | Out-Null
}
Write-Host "\\$env:COMPUTERNAME\$ShareName\Evidence ready"
