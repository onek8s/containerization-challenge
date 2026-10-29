<#
.SYNOPSIS
    Creates the DNS A records used by Supplier Invoices. Run on the DNS server.
    On a single-server installation every name points to the same address.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -WebIP 10.10.1.20 -GatewayIP 10.10.1.21 -ProcessorIP 10.10.1.22 -ErpLinkIP 10.10.1.23 -SqlServerIP 10.10.1.30 -FileServerIP 10.10.1.31 -FtpServerIP 10.10.1.40 -ErpServerIP 10.10.1.50
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$AllIP,
    [string]$WebIP = $AllIP,
    [string]$GatewayIP = $AllIP,
    [string]$ProcessorIP = $AllIP,
    [string]$ErpLinkIP = $AllIP,
    [string]$SqlServerIP = $AllIP,
    [string]$FileServerIP = $AllIP,
    [string]$FtpServerIP = $AllIP,
    [string]$ErpServerIP = $AllIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue)) {
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns"
}

$records = [ordered]@{
    'invoices'      = $WebIP
    'invoices-gw'   = $GatewayIP
    'invoices-proc' = $ProcessorIP
    'invoices-erp'  = $ErpLinkIP
    'sql01'         = $SqlServerIP
    'files01'       = $FileServerIP
    'ftp'           = $FtpServerIP
    'erp01'         = $ErpServerIP
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
