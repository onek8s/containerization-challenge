<#
.SYNOPSIS
    Creates the DNS A records used by Property Inspections. Run on the DNS server.
    Every name may point to the same address on a single-server installation.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP 192.168.8.136 -ServiceIP 192.168.8.136 -AppServerIP 192.168.8.136 -SqlServerIP 192.168.8.136 -FileServerIP 192.168.8.136 -SmtpServerIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$PortalIP,
    [Parameter(Mandatory)] [string]$ServiceIP,
    [Parameter(Mandatory)] [string]$AppServerIP,
    [string]$SqlServerIP,
    [string]$FileServerIP,
    [string]$SmtpServerIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'inspections'     = $PortalIP
    'inspections-svc' = $ServiceIP
    'inspections-app' = $AppServerIP
    'sql01'           = $SqlServerIP
    'files01'         = $FileServerIP
    'smtp'            = $SmtpServerIP
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
