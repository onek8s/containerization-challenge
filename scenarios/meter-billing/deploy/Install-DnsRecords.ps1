<#
.SYNOPSIS
    Creates the DNS A records used by Meter Billing. Run on the DNS server.
    On a single-server installation every name points to the same address.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -LookupIP 10.10.1.60 -EngineIP 10.10.1.61 -SqlServerIP 10.10.1.30 -FileServerIP 10.10.1.31
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$AllIP,
    [string]$LookupIP = $AllIP,
    [string]$EngineIP = $AllIP,
    [string]$SqlServerIP = $AllIP,
    [string]$FileServerIP = $AllIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'billing'        = $LookupIP
    'billing-engine' = $EngineIP
    'sql01'          = $SqlServerIP
    'files01'        = $FileServerIP
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
