<#
.SYNOPSIS
    Creates the read file and output shares on the file server (files01).

    MeterReads$  Inbox\Handheld, Inbox\Amr  written by the docking stations and the AMR collection PC
                 Processed, Rejected        written by the billing engine
    BillingOut$  Print, Print\Reprint        picked up by the print vendor transfer
                 Bank                        picked up by the bank transfer
.EXAMPLE
    .\New-BillingShares.ps1 -Root D:\Shares -EngineAccount CORP\svc-billing -ReadWriters CORP\GG-Meter-Read-Upload -TransferAccount CORP\svc-transfer -OperatorsGroup CORP\Billing-Operators
#>
param(
    [Parameter(Mandatory)] [string]$Root,
    [Parameter(Mandatory)] [string]$EngineAccount,
    [Parameter(Mandatory)] [string]$ReadWriters,
    [Parameter(Mandatory)] [string]$TransferAccount,
    [Parameter(Mandatory)] [string]$OperatorsGroup
)

$ErrorActionPreference = 'Stop'

function New-Folder([string]$path) {
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path | Out-Null }
}

function Grant([string]$path, [string]$identity, [string]$rights) {
    & icacls $path /grant "${identity}:(OI)(CI)$rights" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls failed on $path for $identity" }
}

$reads = Join-Path $Root 'MeterReads'
$out = Join-Path $Root 'BillingOut'

foreach ($folder in @("$reads\Inbox\Handheld", "$reads\Inbox\Amr", "$reads\Processed", "$reads\Rejected", "$out\Print\Reprint", "$out\Bank")) {
    New-Folder $folder
}

& icacls $reads /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null
& icacls $out /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null

Grant $reads $EngineAccount 'M'
Grant $reads $OperatorsGroup 'RX'
Grant "$reads\Inbox" $ReadWriters 'M'
Grant $out $EngineAccount 'M'
Grant $out $TransferAccount 'M'
Grant $out $OperatorsGroup 'RX'

if (-not (Get-SmbShare -Name 'MeterReads$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'MeterReads$' -Path $reads -ChangeAccess $EngineAccount, $ReadWriters -ReadAccess $OperatorsGroup | Out-Null
}

if (-not (Get-SmbShare -Name 'BillingOut$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'BillingOut$' -Path $out -ChangeAccess $EngineAccount, $TransferAccount -ReadAccess $OperatorsGroup | Out-Null
}

Write-Host "Shares \\$env:COMPUTERNAME\MeterReads$ and \\$env:COMPUTERNAME\BillingOut$ ready"
