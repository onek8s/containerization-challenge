<#
.SYNOPSIS
    Creates the drop and archive shares on the file server (files01.<zone>).
    Suppliers' drop folders are created from the channel list; the scanning service writes to Scanning\<code>.
.EXAMPLE
    .\New-InvoiceShares.ps1 -Root D:\Shares -GatewayAccount CORP\svc-inv-gw -ProcessorAccount CORP\svc-inv-proc -WebAccount CORP\svc-inv-web -DropWriters CORP\GG-Invoice-Drop
#>
param(
    [string]$Root = 'D:\Shares',
    [Parameter(Mandatory)] [string]$GatewayAccount,
    [Parameter(Mandatory)] [string]$ProcessorAccount,
    [Parameter(Mandatory)] [string]$WebAccount,
    [string[]]$DropWriters = @(),
    [string[]]$SupplierFolders = @('BP', 'LOS', 'Scanning\SEC')
)

$ErrorActionPreference = 'Stop'

function New-Share([string]$Name, [string]$Path, [string[]]$Change, [string[]]$Read) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    if (-not (Get-SmbShare -Name $Name -ErrorAction SilentlyContinue)) {
        $params = @{ Name = $Name; Path = $Path; FolderEnumerationMode = 'AccessBased' }
        if ($Change) { $params.ChangeAccess = $Change }
        if ($Read) { $params.ReadAccess = $Read }
        New-SmbShare @params | Out-Null
    }
    icacls $Path /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null
    foreach ($a in $Change) { icacls $Path /grant "${a}:(OI)(CI)M" | Out-Null }
    foreach ($a in $Read) { icacls $Path /grant "${a}:(OI)(CI)RX" | Out-Null }
    Write-Host "\\$env:COMPUTERNAME\$Name -> $Path"
}

$drop = Join-Path $Root 'InvoiceDrop'
New-Share 'InvoiceDrop$' $drop -Change (@($GatewayAccount) + $DropWriters) -Read @()
foreach ($folder in $SupplierFolders) {
    New-Item -ItemType Directory -Force -Path (Join-Path $drop $folder), (Join-Path $drop "$folder\processed"), (Join-Path $drop "$folder\rejected") | Out-Null
}

# Originals are written once by the gateway and read by the processor and the web application.
$archive = Join-Path $Root 'InvoiceArchive'
New-Share 'InvoiceArchive$' $archive -Change @($GatewayAccount) -Read @($ProcessorAccount, $WebAccount)
