<#
.SYNOPSIS
    Adds an account to a user right without removing the accounts that already have it.
.DESCRIPTION
    The signing host account needs SeServiceLogonRight (Log on as a service) and SeAuditPrivilege
    (Generate security audits).
.EXAMPLE
    .\grant-user-right.ps1 -Account 'CORP\svc-release-sign' -Right SeServiceLogonRight
    .\grant-user-right.ps1 -Account 'CORP\svc-release-sign' -Right SeAuditPrivilege
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Account,
    [ValidateSet('SeServiceLogonRight', 'SeAuditPrivilege', 'SeBatchLogonRight')] [string] $Right = 'SeServiceLogonRight'
)

$ErrorActionPreference = 'Stop'

$sid = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$work = Join-Path $env:TEMP ("rights-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$export = Join-Path $work 'export.inf'
$import = Join-Path $work 'import.inf'
$sdb = Join-Path $work 'secedit.sdb'

try {
    & secedit.exe /export /cfg $export /areas USER_RIGHTS | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'secedit /export failed.' }

    # secedit writes UTF-16 LE.
    $lines = Get-Content -Path $export -Encoding Unicode
    $line = $lines | Where-Object { $_ -match "^$Right\s*=" } | Select-Object -First 1
    $current = @()
    if ($line) {
        $current = ($line -split '=', 2)[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }
    if ($current -contains "*$sid" -or $current -contains $Account) {
        Write-Host "$Account already has $Right."
        return
    }

    $merged = ($current + "*$sid") -join ','
    $content = @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', "$Right = $merged")
    Set-Content -Path $import -Value $content -Encoding Unicode

    & secedit.exe /configure /db $sdb /cfg $import /areas USER_RIGHTS | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'secedit /configure failed.' }
    Write-Host "Granted $Right to $Account (now $($current.Count + 1) entries)."
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
