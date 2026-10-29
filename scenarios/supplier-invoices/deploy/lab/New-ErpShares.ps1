<#
.SYNOPSIS
    Creates the Import and Export shares of the ERP server for the lab (\\erp01\Import\AP and \\erp01\Export\AP).
.EXAMPLE
    .\New-ErpShares.ps1 -Root D:\Erp -ErpLinkAccount CORP\svc-inv-erp
#>
param(
    [string]$Root = 'D:\Erp',
    [Parameter(Mandatory)] [string]$ErpLinkAccount
)

$ErrorActionPreference = 'Stop'
foreach ($name in 'Import', 'Export') {
    $path = Join-Path $Root $name
    New-Item -ItemType Directory -Force -Path (Join-Path $path 'AP') | Out-Null
    if (-not (Get-SmbShare -Name $name -ErrorAction SilentlyContinue)) {
        New-SmbShare -Name $name -Path $path -ChangeAccess $ErpLinkAccount | Out-Null
    }
    icacls $path /grant "${ErpLinkAccount}:(OI)(CI)M" | Out-Null
    Write-Host "\\$env:COMPUTERNAME\$name -> $path"
}
