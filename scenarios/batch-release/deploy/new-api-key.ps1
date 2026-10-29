<#
.SYNOPSIS
    Creates an API key for the MES (submit) or ERP (read) and stores its hash.
.DESCRIPTION
    The key is shown once. Keys look like br_<8 chars>_<secret>; only the SHA-256 is stored.
.EXAMPLE
    .\new-api-key.ps1 -Name 'MES Turku' -Submit -SiteCode TKU
    .\new-api-key.ps1 -Name 'ERP' -Read
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Name,
    [switch] $Submit,
    [switch] $Read,
    [string] $SiteCode,
    [string] $SqlInstance = 'release-sql.corp.local',
    [string] $DatabaseName = 'BatchRelease'
)

$ErrorActionPreference = 'Stop'
if (-not ($Submit -or $Read)) { throw 'Give -Submit, -Read or both.' }

$alphabet = 'abcdefghijkmnpqrstuvwxyz23456789ABCDEFGHJKLMNPQRSTUVWXYZ'
function Random-Text([int] $Length) {
    $bytes = New-Object byte[] $Length
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

$prefix = (Random-Text 8).ToLowerInvariant()
$key = "br_${prefix}_$(Random-Text 32)"
$sha = [Security.Cryptography.SHA256]::Create()
$hash = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key)) | ForEach-Object { $_.ToString('x2') })

$connection = New-Object System.Data.SqlClient.SqlConnection("Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true")
$connection.Open()
try {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'INSERT br.ApiClient (Name, KeyHash, KeyPrefix, CanSubmit, CanRead, SiteCode, Active) VALUES (@n, @h, @p, @s, @r, @site, 1)'
    [void]$cmd.Parameters.AddWithValue('@n', $Name)
    [void]$cmd.Parameters.AddWithValue('@h', $hash)
    [void]$cmd.Parameters.AddWithValue('@p', $prefix)
    [void]$cmd.Parameters.AddWithValue('@s', [bool]$Submit)
    [void]$cmd.Parameters.AddWithValue('@r', [bool]$Read)
    [void]$cmd.Parameters.AddWithValue('@site', $(if ($SiteCode) { $SiteCode } else { [DBNull]::Value }))
    [void]$cmd.ExecuteNonQuery()
}
finally {
    $connection.Dispose()
}
Write-Host "API key for ${Name}: $key"
