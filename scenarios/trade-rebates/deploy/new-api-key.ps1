<#
.SYNOPSIS
    Creates an API key for a distributor. The key is shown once; only its SHA-256 hash is stored.
.EXAMPLE
    .\new-api-key.ps1 -DistributorCode KESPRO -Name 'Kespro EDI gateway'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $DistributorCode,
    [Parameter(Mandatory)] [string] $Name,
    [string] $SqlInstance = 'rebates-sql.corp.local',
    [string] $DatabaseName = 'TradeRebates'
)

$ErrorActionPreference = 'Stop'
$alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 40
$rng.GetBytes($bytes)
$key = 'tr_' + (-join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] }))

$sha = [System.Security.Cryptography.SHA256]::Create()
$hash = ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key)) | ForEach-Object { $_.ToString('x2') }) -join ''
$prefix = $key.Substring(0, 8)

$query = "SET NOCOUNT ON; EXEC rebate.CreateApiClient @DistributorCode = N'$($DistributorCode.Replace("'", "''"))', @Name = N'$($Name.Replace("'", "''"))', @KeyPrefix = '$prefix', @KeyHash = 0x$hash;"
& sqlcmd.exe -S $SqlInstance -E -C -b -I -d $DatabaseName -Q $query | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Creating the API client failed.' }

Write-Host "API key for $DistributorCode (store it now, it cannot be shown again):"
Write-Host $key
