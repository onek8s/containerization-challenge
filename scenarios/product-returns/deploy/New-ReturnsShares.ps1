<#
.SYNOPSIS
    Creates the file shares on the file server: Returns$ (label PDFs) and Finance$ (refund batches).
    Run as administrator on files01.
.EXAMPLE
    .\New-ReturnsShares.ps1 -Domain CORP -Root D:\Shares
#>
param(
    [string]$Domain = 'CORP',
    [string]$Root = 'D:\Shares'
)

$ErrorActionPreference = 'Stop'

$labels = Join-Path $Root 'Returns\Labels'
$refunds = Join-Path $Root 'Finance\Refunds\Inbound'
New-Item -ItemType Directory -Force -Path $labels, $refunds | Out-Null

if (-not (Get-SmbShare -Name 'Returns$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'Returns$' -Path (Join-Path $Root 'Returns') -Description 'Product Returns labels' `
        -ChangeAccess "$Domain\svc-returns-labels", "$Domain\svc-returns-jobs" -ReadAccess "$Domain\svc-returns-api" | Out-Null
}
if (-not (Get-SmbShare -Name 'Finance$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'Finance$' -Path (Join-Path $Root 'Finance') -Description 'Finance exchange' `
        -ChangeAccess "$Domain\svc-returns-jobs", "$Domain\AP-Refunds" | Out-Null
}

icacls (Join-Path $Root 'Returns') /grant "$Domain\svc-returns-labels:(OI)(CI)M" "$Domain\svc-returns-jobs:(OI)(CI)M" "$Domain\svc-returns-api:(OI)(CI)RX" | Out-Null
icacls (Join-Path $Root 'Finance\Refunds') /grant "$Domain\svc-returns-jobs:(OI)(CI)M" "$Domain\AP-Refunds:(OI)(CI)M" | Out-Null

Get-SmbShare -Name 'Returns$', 'Finance$'
