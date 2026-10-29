<#
.SYNOPSIS
    Creates the statement shares on the file server (files01):
      Statements$        customer drop folders (Customers\<folder>) and print packs (Print\yyyyMM)
      StatementArchive$  copy of every distributed document (yyyy\MM\runNNNNNN)
      Finance$\AR\Daily  daily ledger workbooks (only the folder; the Finance$ share usually exists)
.EXAMPLE
    .\New-StatementShares.ps1 -DistributionAccount CORP\svc-stm-dist -PortalAccount CORP\svc-stm-web -CreditControlGroup CORP\STM-CreditControl -PrintRoomGroup CORP\PrintRoom
#>
param(
    [Parameter(Mandatory)] [string]$DistributionAccount,
    [Parameter(Mandatory)] [string]$PortalAccount,
    [Parameter(Mandatory)] [string]$CreditControlGroup,
    [string]$PrintRoomGroup,
    [string]$Root = 'D:\Shares',
    [string]$FinancePath = 'D:\Shares\Finance'
)

$ErrorActionPreference = 'Stop'

$statements = Join-Path $Root 'Statements'
$archive = Join-Path $Root 'StatementArchive'
New-Item -ItemType Directory -Force -Path (Join-Path $statements 'Customers'), (Join-Path $statements 'Print'), $archive, (Join-Path $FinancePath 'AR\Daily') | Out-Null

function Set-Share([string]$name, [string]$path, [string[]]$change, [string[]]$read) {
    if (-not (Get-SmbShare -Name $name -ErrorAction SilentlyContinue)) {
        $params = @{ Name = $name; Path = $path; FolderEnumerationMode = 'AccessBased' }
        if ($change) { $params.ChangeAccess = $change }
        if ($read) { $params.ReadAccess = $read }
        New-SmbShare @params | Out-Null
    }
    foreach ($a in $change) { icacls $path /grant "${a}:(OI)(CI)M" | Out-Null }
    foreach ($a in $read) { icacls $path /grant "${a}:(OI)(CI)RX" | Out-Null }
    Write-Host "\\$env:COMPUTERNAME\$name -> $path"
}

$read = @($CreditControlGroup)
if ($PrintRoomGroup) { $read += $PrintRoomGroup }
Set-Share 'Statements$' $statements @($DistributionAccount) $read
Set-Share 'StatementArchive$' $archive @($DistributionAccount) @($PortalAccount, $CreditControlGroup)
icacls (Join-Path $FinancePath 'AR\Daily') /grant "${DistributionAccount}:(OI)(CI)M" | Out-Null
Write-Host 'Customer drop folders are created by the distribution service on first delivery. Grant each customer''s SFTP or partner account read access to its own folder.'
