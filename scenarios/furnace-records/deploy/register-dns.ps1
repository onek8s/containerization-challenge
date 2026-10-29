<#
.SYNOPSIS
    Registers the Furnace Records host names in the DNS zone.
.DESCRIPTION
    Run on the DNS server. In a lab all names can point to one server.
.EXAMPLE
    .\register-dns.ps1 -ZoneName corp.local -IPAddress 192.168.8.136
.EXAMPLE
    .\register-dns.ps1 -IPAddress 10.20.0.15 -DaqIPAddress 10.20.4.21 -HmiIPAddress 10.20.4.30 -SqlIPAddress 10.20.0.40 -FilesIPAddress 10.20.0.41
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $PortalHost = 'furnace',
    [string] $ApiHost = 'furnace-api',
    [string] $WorkerHost = 'furnace-app01',
    [string] $DaqHost = 'daq01',
    [string] $HmiHost = 'furnace-hmi01',
    [string] $SqlHost = 'sql01',
    [string] $FilesHost = 'files01',
    [string] $WorkerIPAddress = $IPAddress,
    [string] $DaqIPAddress = $IPAddress,
    [string] $HmiIPAddress = $IPAddress,
    [string] $SqlIPAddress = $IPAddress,
    [string] $FilesIPAddress = $IPAddress,
    [switch] $SkipSharedHosts
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = @(
    @{ Name = $PortalHost; IP = $IPAddress },
    @{ Name = $ApiHost; IP = $IPAddress },
    @{ Name = $WorkerHost; IP = $WorkerIPAddress },
    @{ Name = $DaqHost; IP = $DaqIPAddress },
    @{ Name = $HmiHost; IP = $HmiIPAddress }
)
if (-not $SkipSharedHosts) {
    # sql01 and files01 are shared with other applications; skip them when they already exist.
    $records += @{ Name = $SqlHost; IP = $SqlIPAddress; Shared = $true }
    $records += @{ Name = $FilesHost; IP = $FilesIPAddress; Shared = $true }
}

foreach ($r in $records) {
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -ErrorAction SilentlyContinue
    if ($existing -and $r.Shared) {
        Write-Host "$($r.Name).$ZoneName exists ($($existing.RecordData.IPv4Address)), kept"
        continue
    }
    if ($existing) {
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $r.Name -IPv4Address $r.IP
    Write-Host "$($r.Name).$ZoneName -> $($r.IP)"
}
