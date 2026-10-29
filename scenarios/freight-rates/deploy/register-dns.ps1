<#
.SYNOPSIS
    Registers the Freight Rates host names in the DNS zone.
.DESCRIPTION
    Run on the DNS server. In a lab all names can point to one server.
.EXAMPLE
    .\register-dns.ps1 -ZoneName corp.local -IPAddress 192.168.8.136
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $ApiHost = 'rates',
    [string] $AdminHost = 'rates-admin',
    [string] $RatingHost = 'ratehost01',
    [string] $SqlHost = 'sql01',
    [string] $RatingHostIPAddress = $IPAddress,
    [string] $SqlIPAddress = $IPAddress
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = @(
    @{ Name = $ApiHost; IP = $IPAddress },
    @{ Name = $AdminHost; IP = $IPAddress },
    @{ Name = $RatingHost; IP = $RatingHostIPAddress },
    @{ Name = $SqlHost; IP = $SqlIPAddress }
)

foreach ($r in $records) {
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $r.Name -IPv4Address $r.IP
    Write-Host "$($r.Name).$ZoneName -> $($r.IP)"
}
