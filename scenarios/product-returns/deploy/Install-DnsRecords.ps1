<#
.SYNOPSIS
    Creates the DNS A records used by Product Returns. Run on the DNS server.
    On a single-server installation every name points to the same address.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -ProxyIP 10.20.1.10 -WebIP 10.20.1.11 -ApiIP 10.20.1.12 -JobsIP 10.20.1.13 -SqlServerIP 10.20.1.30 -FileServerIP 10.20.1.31 -SmtpIP 10.20.1.40
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$AllIP,
    [string]$ProxyIP = $AllIP,
    [string]$WebIP = $AllIP,
    [string]$ApiIP = $AllIP,
    [string]$JobsIP = $AllIP,
    [string]$SqlServerIP = $AllIP,
    [string]$FileServerIP = $AllIP,
    [string]$SmtpIP = $AllIP,
    [string]$WebProxyIP = $AllIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'returns'      = $ProxyIP
    'returns-web'  = $WebIP
    'returns-api'  = $ApiIP
    'returns-jobs' = $JobsIP
    'sql01'        = $SqlServerIP
    'files01'      = $FileServerIP
    'smtp'         = $SmtpIP
    'proxy'        = $WebProxyIP
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
