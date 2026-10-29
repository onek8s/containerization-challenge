# Shared helpers for the Dealer Orders deployment scripts. Dot-source: . "$PSScriptRoot\common.ps1"

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:InstallRoot = 'D:\DealerOrders'

$script:WebApps = @{
    portal    = @{ Project = 'Tailspin.DealerOrders.DealerPortal';    HostName = 'dealers';   SqlLogin = 'dlr_portal';    Pool = 'DealerPortal';    WindowsAuth = $false; Runtime = 'v4.0' }
    salesdesk = @{ Project = 'Tailspin.DealerOrders.SalesDesk';       HostName = 'salesdesk'; SqlLogin = 'dlr_salesdesk'; Pool = 'DealerSalesDesk'; WindowsAuth = $true;  Runtime = 'v4.0' }
    docs      = @{ Project = 'Tailspin.DealerOrders.DocumentService'; HostName = 'docs';      SqlLogin = 'dlr_docs';      Pool = 'DealerDocuments'; WindowsAuth = $false; Runtime = 'v4.0' }
}

$script:Services = @{
    AxConnector   = @{ Project = 'Tailspin.DealerOrders.AxConnector';   ServiceName = 'TailspinDealerAxConnector';   SqlLogin = 'dlr_axconnector'; Server = 'axint01'; NeedsAxClient = $true;  UrlAcl = $null }
    PriceSync     = @{ Project = 'Tailspin.DealerOrders.PriceSync';     ServiceName = 'TailspinDealerPriceSync';     SqlLogin = 'dlr_pricesync';   Server = 'axint01'; NeedsAxClient = $true;  UrlAcl = $null }
    EdiGateway    = @{ Project = 'Tailspin.DealerOrders.EdiGateway';    ServiceName = 'TailspinDealerEdiGateway';    SqlLogin = 'dlr_edi';         Server = 'edi01';   NeedsAxClient = $false; UrlAcl = 'http://+:4080/' }
    Notifications = @{ Project = 'Tailspin.DealerOrders.Notifications'; ServiceName = 'TailspinDealerNotifications'; SqlLogin = 'dlr_notify';      Server = 'app01';   NeedsAxClient = $false; UrlAcl = $null }
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

function Test-NetFramework472 {
    # Release key 461808 = .NET Framework 4.7.2
    $release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
    return ($release -ge 461808)
}

function New-SqlConnectionString {
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$User,
        [Parameter(Mandatory)][object]$Password,
        [Parameter(Mandatory)][string]$ApplicationName
    )
    $pw = ConvertTo-PlainText $Password
    "Data Source=$Server;Initial Catalog=DealerOrders;User ID=$User;Password=$pw;Application Name=$ApplicationName"
}

# Sets appSettings keys and the DealerOrders connection string in a .NET Framework config file (App.config or Web.config).
function Set-ConfigValues {
    param(
        [Parameter(Mandatory)][string]$Path,
        [hashtable]$AppSettings = @{},
        [string]$ConnectionString
    )
    [xml]$xml = Get-Content $Path -Raw
    foreach ($key in $AppSettings.Keys) {
        $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
        if (-not $node) {
            $node = $xml.CreateElement('add')
            $node.SetAttribute('key', $key)
            [void]$xml.configuration.appSettings.AppendChild($node)
        }
        $node.SetAttribute('value', [string]$AppSettings[$key])
    }
    if ($ConnectionString) {
        $cs = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'DealerOrders' }
        $cs.SetAttribute('connectionString', $ConnectionString)
    }
    $xml.Save($Path)
}

# Grants a user right (SeServiceLogonRight, SeBatchLogonRight, ...) with secedit, keeping the accounts already listed.
function Grant-UserRight {
    param(
        [Parameter(Mandatory)][string]$Account,
        [Parameter(Mandatory)][string]$Right
    )
    $sid = (New-Object Security.Principal.NTAccount($Account)).Translate([Security.Principal.SecurityIdentifier]).Value
    $temp = Join-Path $env:TEMP ([Guid]::NewGuid().ToString('N'))
    New-Item $temp -ItemType Directory | Out-Null
    try {
        secedit /export /cfg "$temp\current.inf" /areas USER_RIGHTS | Out-Null
        $line = Select-String -Path "$temp\current.inf" -Pattern "^$Right\s*=" | Select-Object -First 1
        $current = if ($line) { ($line.Line -split '=', 2)[1].Trim() } else { '' }
        if ($current -match [regex]::Escape("*$sid")) { Write-Host "  $Account already has $Right"; return }
        $value = if ($current) { "$current,*$sid" } else { "*$sid" }
        @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', "$Right = $value") |
            Set-Content "$temp\grant.inf" -Encoding Unicode
        secedit /configure /db "$temp\grant.sdb" /cfg "$temp\grant.inf" /areas USER_RIGHTS | Out-Null
        Write-Host "  $Right granted to $Account"
    }
    finally {
        Remove-Item $temp -Recurse -Force
    }
}

function Invoke-SqlScript {
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$File,
        [hashtable]$Variables = @{}
    )
    $args = @('-S', $Server, '-E', '-b', '-I', '-i', $File)
    foreach ($k in $Variables.Keys) { $args += @('-v', "$k=$(ConvertTo-PlainText $Variables[$k])") }
    & sqlcmd.exe @args
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for $File" }
}
