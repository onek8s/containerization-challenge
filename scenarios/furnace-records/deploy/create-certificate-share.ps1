<#
.SYNOPSIS
    Creates the certificate archive share on the file server.
.EXAMPLE
    .\create-certificate-share.ps1 -Path D:\Shares\FurnaceRecords -WorkerAccount 'CORP\svc-furnace-wrk$' -ReadGroup 'CORP\FR-Quality'
#>
param(
    [string] $Path = 'D:\Shares\FurnaceRecords',
    [string] $ShareName = 'FurnaceRecords$',
    [Parameter(Mandatory)] [string] $WorkerAccount,
    [string] $ReadGroup = 'CORP\FR-Quality'
)

$ErrorActionPreference = 'Stop'

$certificates = Join-Path $Path 'Certificates'
New-Item -ItemType Directory -Force -Path $certificates | Out-Null

# Certificates are written once and kept; the worker may create but not delete.
icacls $Path /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null
icacls $Path /grant "${WorkerAccount}:(OI)(CI)(RX,W)" "${ReadGroup}:(OI)(CI)RX" | Out-Null

if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $ShareName -Path $Path -ChangeAccess $WorkerAccount -ReadAccess $ReadGroup -FullAccess 'BUILTIN\Administrators' | Out-Null
}
Get-SmbShare -Name $ShareName
