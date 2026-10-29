<#
.SYNOPSIS
    Registers the Study Data host names in the DNS zone.
.DESCRIPTION
    Run on the DNS server (domain controller). In a lab all names can point to one server.
.EXAMPLE
    .\register-dns.ps1 -IPAddress 192.168.8.136
.EXAMPLE
    .\register-dns.ps1 -IPAddress 10.20.0.31 -AppServerIPAddress 10.20.0.32 -SqlIPAddress 10.20.0.40 -FilesIPAddress 10.20.0.41 -AdfsIPAddress 10.20.0.12
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $EdcHost = 'edc',
    [string] $MonitorHost = 'monitor',
    [string] $SponsorHost = 'sponsor-api',
    [string] $AppServerHost = 'trials-app01',
    [string] $AppServerIPAddress = $IPAddress,
    [string] $SqlHost = 'sql01',
    [string] $SqlIPAddress = $IPAddress,
    [string] $FilesHost = 'files01',
    [string] $FilesIPAddress = $IPAddress,
    [string] $AdfsHost = 'adfs',
    [string] $AdfsIPAddress = $IPAddress,
    [switch] $SkipSharedHosts
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = @(
    @{ Name = $EdcHost; IP = $IPAddress },
    @{ Name = $MonitorHost; IP = $IPAddress },
    @{ Name = $SponsorHost; IP = $IPAddress },
    @{ Name = $AppServerHost; IP = $AppServerIPAddress }
)
if (-not $SkipSharedHosts) {
    # sql01, files01 and adfs are shared with other applications; existing records are kept.
    $records += @{ Name = $SqlHost; IP = $SqlIPAddress }
    $records += @{ Name = $FilesHost; IP = $FilesIPAddress }
    $records += @{ Name = $AdfsHost; IP = $AdfsIPAddress }
}

foreach ($r in $records) {
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "$($r.Name).$ZoneName exists ($($existing.RecordData.IPv4Address)), kept"
        continue
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $r.Name -IPv4Address $r.IP
    Write-Host "$($r.Name).$ZoneName -> $($r.IP)"
}
