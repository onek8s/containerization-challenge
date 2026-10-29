# Shared helpers for the Control Tower deployment scripts. Dot-source: . "$PSScriptRoot\common.ps1"

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ControlTowerRoot = 'D:\ControlTower'

# Application catalogue: IIS sites and Windows services, their binaries and accounts.
$script:WebApps = @{
    login   = @{ Project = 'ControlTower.Identity';      Dll = 'ControlTower.Identity.dll';      SqlLogin = 'ct_identity'; Database = 'ControlTowerIdentity'; ConnectionName = 'Identity'; MqUser = $null }
    booking = @{ Project = 'ControlTower.Booking.Web';   Dll = 'ControlTower.Booking.Web.dll';   SqlLogin = 'ct_booking';  Database = 'ControlTower';         ConnectionName = 'ControlTower'; MqUser = 'ct-booking' }
    track   = @{ Project = 'ControlTower.Portal';        Dll = 'ControlTower.Portal.dll';        SqlLogin = 'ct_portal';   Database = 'ControlTower';         ConnectionName = 'ControlTower'; MqUser = 'ct-portal' }
    tower   = @{ Project = 'ControlTower.Tower.Api';     Dll = 'ControlTower.Tower.Api.dll';     SqlLogin = 'ct_tower';    Database = 'ControlTower';         ConnectionName = 'ControlTower'; MqUser = 'ct-tower' }
    scan    = @{ Project = 'ControlTower.Scanning.Api';  Dll = 'ControlTower.Scanning.Api.dll';  SqlLogin = 'ct_scan';     Database = 'ControlTower';         ConnectionName = 'ControlTower'; MqUser = 'ct-scan' }
}

$script:Services = @{
    CarrierGateway = @{ Project = 'ControlTower.CarrierGateway'; Exe = 'ControlTower.CarrierGateway.exe'; ServiceName = 'ControlTowerCarrierGateway'; DisplayName = 'Control Tower Carrier Gateway'; SqlLogin = 'ct_gateway';    MqUser = 'ct-gateway';    Ports = @(8085) }
    Telematics     = @{ Project = 'ControlTower.Telematics';     Exe = 'ControlTower.Telematics.exe';     ServiceName = 'ControlTowerTelematics';     DisplayName = 'Control Tower Telematics';      SqlLogin = 'ct_telematics'; MqUser = 'ct-telematics'; Ports = @() }
    Events         = @{ Project = 'ControlTower.Events';         Exe = 'ControlTower.Events.exe';         ServiceName = 'ControlTowerEvents';         DisplayName = 'Control Tower Event Processing'; SqlLogin = 'ct_events';    MqUser = 'ct-events';     Ports = @() }
    Jobs           = @{ Project = 'ControlTower.Jobs';           Exe = 'ControlTower.Jobs.exe';           ServiceName = 'ControlTowerJobs';           DisplayName = 'Control Tower Jobs';            SqlLogin = 'ct_jobs';      MqUser = 'ct-jobs';       Ports = @(5080) }
}

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Admin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script in an elevated PowerShell session.'
    }
}

function ConvertTo-PlainText {
    param([Parameter(Mandatory)][object]$Value)
    if ($Value -is [securestring]) {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
        try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    }
    return [string]$Value
}

function New-SqlConnectionString {
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$User,
        [Parameter(Mandatory)][object]$Password,
        [Parameter(Mandatory)][string]$ApplicationName
    )
    $pw = ConvertTo-PlainText $Password
    "Server=$Server;Database=$Database;User ID=$User;Password=$pw;Encrypt=True;TrustServerCertificate=True;Application Name=$ApplicationName"
}

function New-RabbitConnectionString {
    param(
        [Parameter(Mandatory)][string]$HostName,
        [Parameter(Mandatory)][string]$User,
        [Parameter(Mandatory)][object]$Password,
        [string]$VirtualHost = 'controltower'
    )
    $pw = ConvertTo-PlainText $Password
    "host=$HostName;virtualHost=$VirtualHost;username=$User;password=$pw;publisherConfirms=true;timeout=30"
}

# Merges "Section:Key" = value pairs into a JSON settings file (created when missing).
# Values may be strings, numbers, booleans or arrays.
function Set-AppSettings {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Values
    )
    if (Test-Path $Path) {
        $json = Get-Content $Path -Raw | ConvertFrom-Json
    } else {
        $json = [pscustomobject]@{}
    }
    foreach ($key in $Values.Keys) {
        $parts = $key -split ':'
        $node = $json
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            $name = $parts[$i]
            if (-not $node.PSObject.Properties[$name]) {
                $node | Add-Member -NotePropertyName $name -NotePropertyValue ([pscustomobject]@{})
            }
            $node = $node.$name
        }
        $leaf = $parts[-1]
        if ($node.PSObject.Properties[$leaf]) {
            $node.$leaf = $Values[$key]
        } else {
            $node | Add-Member -NotePropertyName $leaf -NotePropertyValue $Values[$key]
        }
    }
    $json | ConvertTo-Json -Depth 20 | Set-Content -Path $Path -Encoding UTF8
}

function Grant-FolderAccess {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Identity,
        [ValidateSet('Read', 'ReadAndExecute', 'Modify', 'FullControl')][string]$Rights = 'Modify'
    )
    if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
    $acl = Get-Acl $Path
    $rule = New-Object Security.AccessControl.FileSystemAccessRule($Identity, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

# Creates (or resets) a local user used as a service account. Domain or managed service accounts
# are passed to the install scripts as DOMAIN\name instead.
function New-ServiceAccount {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][securestring]$Password,
        [string]$Description = 'Control Tower service account'
    )
    $user = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue
    if ($user) {
        Set-LocalUser -Name $Name -Password $Password -PasswordNeverExpires $true
    } else {
        New-LocalUser -Name $Name -Password $Password -Description $Description -PasswordNeverExpires -UserMayNotChangePassword | Out-Null
    }
    "$env:COMPUTERNAME\$Name"
}

# Adds an account to a user right (e.g. SeServiceLogonRight) without removing the existing holders.
function Grant-UserRight {
    param(
        [Parameter(Mandatory)][string]$Account,
        [Parameter(Mandatory)][string]$Right
    )
    $sid = (New-Object Security.Principal.NTAccount($Account)).Translate([Security.Principal.SecurityIdentifier]).Value
    $temp = Join-Path $env:TEMP ("secpol-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        $export = Join-Path $temp 'export.inf'
        & secedit.exe /export /cfg $export /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit export failed ($LASTEXITCODE)" }

        $lines = Get-Content $export -Encoding Unicode
        $existing = $lines | Where-Object { $_ -match "^$Right\s*=" } | Select-Object -First 1
        if ($existing) {
            if ($existing -match [regex]::Escape("*$sid")) {
                Write-Host "  $Account already has $Right"
                return
            }
            $newLine = "$existing,*$sid"
        } else {
            $newLine = "$Right = *$sid"
        }

        $import = Join-Path $temp 'import.inf'
        @(
            '[Unicode]',
            'Unicode=yes',
            '[Version]',
            'signature="$CHICAGO$"',
            'Revision=1',
            '[Privilege Rights]',
            $newLine
        ) | Set-Content -Path $import -Encoding Unicode

        $db = Join-Path $temp 'secedit.sdb'
        & secedit.exe /configure /db $db /cfg $import /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit configure failed ($LASTEXITCODE)" }
        Write-Host "  Granted $Right to $Account"
    } finally {
        Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
    }
}

function Test-HostingBundle {
    param([int]$MajorVersion = 6)
    $key = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Updates\.NET'
    $module = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
    $runtime = Get-ChildItem (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.AspNetCore.App') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "$MajorVersion.*" }
    return (Test-Path $module) -and ($null -ne $runtime)
}

function Test-DotNetRuntime {
    param([int]$MajorVersion = 6)
    $runtime = Get-ChildItem (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "$MajorVersion.*" }
    return $null -ne $runtime
}
