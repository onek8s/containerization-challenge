<#
.SYNOPSIS
    Registers a warehouse handheld scanner and prints its API key. Only the SHA-256 hash of the
    key is stored. Enter the key in the scanner app settings (Returns > API key).
.EXAMPLE
    .\New-ScannerKey.ps1 -Name RTN-DOCK-03 -Location "Rotterdam returns dock 3"
#>
param(
    [Parameter(Mandatory)] [string]$Name,
    [string]$Location = '',
    [string]$SqlServer = 'sql01.corp.local',
    [string]$DatabaseName = 'ProductReturns'
)

$ErrorActionPreference = 'Stop'

$bytes = New-Object byte[] 24
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')

$sha = [System.Security.Cryptography.SHA256]::Create()
$hash = -join ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key)) | ForEach-Object { $_.ToString('x2') })

$query = @"
MERGE rtn.ScannerDevice AS t
USING (SELECT N'$($Name.Replace("'", "''"))' AS Name) AS s ON t.Name = s.Name
WHEN MATCHED THEN UPDATE SET ApiKeyHash = N'$hash', Location = N'$($Location.Replace("'", "''"))', Enabled = 1
WHEN NOT MATCHED THEN INSERT (Name, Location, ApiKeyHash, Enabled) VALUES (s.Name, N'$($Location.Replace("'", "''"))', N'$hash', 1);
"@
& sqlcmd -S $SqlServer -E -b -d $DatabaseName -Q $query
if ($LASTEXITCODE -ne 0) { throw 'Registering the scanner failed.' }

Write-Host "Scanner $Name registered. API key (shown once):"
Write-Host $key
