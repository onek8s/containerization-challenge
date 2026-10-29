<#
.SYNOPSIS
    Registers the Broker Quotes host names in the AD-integrated DNS zone.
.DESCRIPTION
    Run on a DNS server of the domain. In the lab all names point to host01.
.EXAMPLE
    .\Register-Dns.ps1 -WebIPAddress 10.20.1.21 -AppIPAddress 10.20.1.31 -SqlIPAddress 10.20.1.40 -MqIPAddress 10.20.1.35
.EXAMPLE
    .\Register-Dns.ps1 -WebIPAddress 192.168.8.136     # lab: every name on one server
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $WebIPAddress,
    [string] $ApiIPAddress = $WebIPAddress,
    [string] $AppIPAddress = $WebIPAddress,
    [string] $SqlIPAddress = $WebIPAddress,
    [string] $MqIPAddress = $WebIPAddress,
    [string] $SmtpIPAddress = $WebIPAddress
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = [ordered]@{
    'login'     = $WebIPAddress   # identity provider
    'brokers'   = $WebIPAddress   # broker portal
    'workbench' = $WebIPAddress   # underwriting workbench
    'quote-api' = $ApiIPAddress   # quote API (RateCore installed here)
    'app01'     = $AppIPAddress   # policy service and documents service (RateCore installed here)
    'sql01'     = $SqlIPAddress
    'mq01'      = $MqIPAddress
    'smtp'      = $SmtpIPAddress
}

foreach ($name in $records.Keys) {
    $ip = $records[$name]
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -eq $ip) {
            Write-Host "$name.$ZoneName already points to $ip"
            continue
        }
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $ip
    Write-Host "$name.$ZoneName -> $ip"
}
