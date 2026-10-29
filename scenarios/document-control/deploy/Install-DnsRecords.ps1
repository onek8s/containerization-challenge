<#
.SYNOPSIS
    Creates the DNS A records used by Document Control. Run on the DNS server.
    On a single-server installation every name points to the same address.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP 10.10.1.60 -PackagerIP 10.10.1.61 -IndexerIP 10.10.1.62 -SqlServerIP 10.10.1.30 -FileServerIP 10.10.1.31 -SmtpIP 10.10.1.25
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$AllIP,
    [string]$PortalIP = $AllIP,
    [string]$PackagerIP = $AllIP,
    [string]$IndexerIP = $AllIP,
    [string]$SqlServerIP = $AllIP,
    [string]$FileServerIP = $AllIP,
    [string]$SmtpIP = $AllIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'doccontrol'     = $PortalIP
    'doccontrol-pkg' = $PackagerIP
    'doccontrol-idx' = $IndexerIP
    'sql01'          = $SqlServerIP
    'files01'        = $FileServerIP
    'smtp'           = $SmtpIP
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
