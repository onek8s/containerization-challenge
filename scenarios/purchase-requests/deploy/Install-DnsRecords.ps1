<#
.SYNOPSIS
    Creates the DNS A records used by Purchase Requests. Run on the DNS server.
    Every name may point to the same address on a single-server installation.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -WebServerIP 192.168.8.136 -ApiServerIP 192.168.8.136 -AppServerIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$WebServerIP,
    [Parameter(Mandatory)] [string]$ApiServerIP,
    [string]$AppServerIP,
    [string]$SqlServerIP,
    [string]$FileServerIP,
    [string]$SmtpServerIP,
    [string]$ErpServerIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = [ordered]@{
    'purchasing'     = $WebServerIP
    'purchasing-api' = $ApiServerIP
    'purchasing-app' = $AppServerIP
    'sql01'          = $SqlServerIP
    'files01'        = $FileServerIP
    'smtp'           = $SmtpServerIP
    'erp01'          = $ErpServerIP
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
