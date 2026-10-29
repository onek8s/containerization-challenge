<#
.SYNOPSIS
    Registers the City Permits host names in the corp.local zone.
.EXAMPLE
    .\Register-Dns.ps1 -DnsServer dc01 -Map @{ permits = '10.20.4.11'; 'permits-office' = '10.20.4.12' }
    .\Register-Dns.ps1 -DnsServer host01 -LabTarget host01.corp.local
#>
param(
    [string] $DnsServer = 'dc01',
    [string] $ZoneName = 'corp.local',
    [hashtable] $Map,
    [string] $LabTarget
)
$ErrorActionPreference = 'Stop'
$names = 'permits', 'permits-office', 'inspect', 'maps', 'opendata', 'permits-app01', 'permits-sql', 'gis-sql', 'records01'
foreach ($name in $names) {
    Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -ErrorAction SilentlyContinue |
        Remove-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Force
    if ($LabTarget) {
        Add-DnsServerResourceRecordCName -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -HostNameAlias $LabTarget
    }
    elseif ($Map -and $Map.ContainsKey($name)) {
        Add-DnsServerResourceRecordA -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -IPv4Address $Map[$name]
    }
    Write-Host "$name.$ZoneName registered"
}
