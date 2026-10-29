<#
.SYNOPSIS
    Adds an account to a user right (for example SeServiceLogonRight) in the local security policy,
    keeping the accounts that already have it. Used by the install scripts.
.EXAMPLE
    .\Set-SecurityPolicyRight.ps1 -Account CORP\svc-dc-packager -Right SeServiceLogonRight
#>
param(
    [Parameter(Mandatory)] [string]$Account,
    [Parameter(Mandatory)] [string]$Right
)

$ErrorActionPreference = 'Stop'

$sid = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$work = Join-Path $env:TEMP ("secpol-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$export = Join-Path $work 'export.inf'
$import = Join-Path $work 'import.inf'
$db = Join-Path $work 'secedit.sdb'

try {
    secedit /export /cfg $export /areas USER_RIGHTS | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "secedit /export failed ($LASTEXITCODE)." }

    $line = Get-Content $export -Encoding Unicode | Where-Object { $_ -match "^$Right\s*=" } | Select-Object -First 1
    $current = @()
    if ($line) {
        $current = ($line -split '=', 2)[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }

    if ($current -contains "*$sid" -or $current -contains $Account) {
        Write-Host "$Account already has $Right"
        return
    }

    $value = (@($current) + "*$sid") -join ','
    @(
        '[Unicode]',
        'Unicode=yes',
        '[Version]',
        'signature="$CHICAGO$"',
        'Revision=1',
        '[Privilege Rights]',
        "$Right = $value"
    ) | Set-Content -Path $import -Encoding Unicode

    secedit /configure /db $db /cfg $import /areas USER_RIGHTS | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "secedit /configure failed ($LASTEXITCODE)." }
    Write-Host "Granted $Right to $Account"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
