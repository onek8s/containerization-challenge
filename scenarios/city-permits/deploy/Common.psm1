function Assert-Administrator {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this script as Administrator.' }
}

function Grant-LogonAsService {
    param([Parameter(Mandatory)] [string] $Account)
    $sid = (New-Object Security.Principal.NTAccount($Account)).Translate([Security.Principal.SecurityIdentifier]).Value
    $tmp = Join-Path $env:TEMP "permits-secedit-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        secedit /export /cfg "$tmp\current.inf" /areas USER_RIGHTS | Out-Null
        $line = Get-Content "$tmp\current.inf" -Encoding Unicode | Where-Object { $_ -like 'SeServiceLogonRight*' }
        if ($line -and $line -match [regex]::Escape("*$sid")) { return }
        $merged = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
        @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $merged) |
            Set-Content "$tmp\merge.inf" -Encoding Unicode
        secedit /configure /db "$tmp\merge.sdb" /cfg "$tmp\merge.inf" /areas USER_RIGHTS | Out-Null
    }
    finally { Remove-Item $tmp -Recurse -Force }
}

function Write-JsonSettings {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [hashtable] $Settings)
    $Settings | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8
}

function Test-GeomatixEngine {
    param([string] $InstallDir = 'C:\Program Files\Geomatix\Pro')
    $core = Join-Path $InstallDir 'bin\Geomatix.Core.Hosting.dll'
    if (-not (Test-Path $core)) { return $false }
    $status = & (Join-Path $InstallDir 'bin\LicenseAdmin.exe') /status /product:Standard 2>$null
    return ($LASTEXITCODE -eq 0 -and "$status" -match 'Authorized')
}

Export-ModuleMember -Function Assert-Administrator, Grant-LogonAsService, Write-JsonSettings, Test-GeomatixEngine
