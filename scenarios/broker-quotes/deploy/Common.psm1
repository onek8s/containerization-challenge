# Shared helpers for the Broker Quotes deploy scripts.

Set-StrictMode -Version Latest

function Assert-Administrator {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script in an elevated PowerShell session.'
    }
}

function Write-ProductionSettings {
    # Writes appsettings.Production.json next to the application. Values given here override appsettings.json.
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [hashtable] $Settings
    )
    $json = $Settings | ConvertTo-Json -Depth 10
    Set-Content -Path (Join-Path $Path 'appsettings.Production.json') -Value $json -Encoding UTF8
}

function Grant-FolderAccess {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Identity,
        [ValidateSet('ReadAndExecute', 'Modify')] [string] $Rights = 'ReadAndExecute'
    )
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl = Get-Acl $Path
    $rule = New-Object Security.AccessControl.FileSystemAccessRule($Identity, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

function Grant-LogonAsService {
    # Adds the account to "Log on as a service" (SeServiceLogonRight). The current assignment is
    # exported and merged; only the one right is applied back, other rights are left as they are.
    param([Parameter(Mandatory)] [string] $Account)

    $sid = (New-Object Security.Principal.NTAccount($Account)).Translate([Security.Principal.SecurityIdentifier]).Value
    $work = Join-Path $env:TEMP ("secedit-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $export = Join-Path $work 'export.inf'
        secedit /export /cfg $export /areas USER_RIGHTS | Out-Null
        $lines = Get-Content $export -Encoding Unicode
        $line = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
        if ($line -and $line -match [regex]::Escape("*$sid")) {
            Write-Host "$Account already has Log on as a service"
            return
        }
        $value = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
        $import = Join-Path $work 'import.inf'
        @(
            '[Unicode]',
            'Unicode=yes',
            '[Version]',
            'signature="$CHICAGO$"',
            'Revision=1',
            '[Privilege Rights]',
            $value
        ) | Set-Content $import -Encoding Unicode
        secedit /configure /db (Join-Path $work 'secedit.sdb') /cfg $import /areas USER_RIGHTS | Out-Null
        Write-Host "Granted Log on as a service to $Account"
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Test-RateCoreRegistered {
    # RateCore is a 32-bit COM server: its registration is in the 32-bit registry view.
    $key = 'Registry::HKEY_CLASSES_ROOT\WOW6432Node\CLSID'
    $progId = Get-Item 'Registry::HKEY_CLASSES_ROOT\RateCore.Engine\CLSID' -ErrorAction SilentlyContinue
    if (-not $progId) { return $false }
    $clsid = $progId.GetValue('')
    return Test-Path (Join-Path $key "$clsid\InprocServer32")
}

Export-ModuleMember -Function Assert-Administrator, Write-ProductionSettings, Grant-FolderAccess, Grant-LogonAsService, Test-RateCoreRegistered
