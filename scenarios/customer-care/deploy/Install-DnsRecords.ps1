<#
.SYNOPSIS
    Creates the DNS A records used by Customer Care. Run on the DNS server.
    On a single-server installation every name points to the same address.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP 192.168.8.140
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP 10.1.4.21 -IntegrationIP 10.1.4.22 -SqlServerIP 10.1.2.10 -SmtpServerIP 10.1.2.25
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$PortalIP,
    [string]$IntegrationIP = $PortalIP,
    [string]$SqlServerIP,
    [string]$SmtpServerIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'care'     = $PortalIP
    'care-api' = $IntegrationIP
    'sql01'    = $SqlServerIP
    'smtp'     = $SmtpServerIP
}

foreach ($name in $records.Keys) {
    $ip = $records[$name]
    if (-not $ip) { continue }

    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -eq $ip) {
            Write-Host "$name.$ZoneName -> $ip (exists)"
            continue
        }
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -Force
    }

    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $ip
    Write-Host "$name.$ZoneName -> $ip"
}
