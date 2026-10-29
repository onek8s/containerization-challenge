<#
.SYNOPSIS
    Creates the DNS A records used by Field Sales. Run on the DNS server (domain controller).
    All names may point to the same server.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -WebServerIP 192.168.8.136 -SqlServerIP 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$WebServerIP,
    [string]$ApiServerIP = $WebServerIP,
    [string]$DeploymentServerIP = $WebServerIP,
    [string]$SqlServerIP,
    [string]$SmtpServerIP,
    [string]$WarehouseServerIP
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = [ordered]@{
    'salesadmin' = $WebServerIP
    'salesapi'   = $ApiServerIP
    'salesapp'   = $DeploymentServerIP
    'sql01'      = $SqlServerIP
    'smtp'       = $SmtpServerIP
    'wms'        = $WarehouseServerIP
}

foreach ($name in $records.Keys) {
    $ip = $records[$name]
    if (-not $ip) { continue }

    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -contains $ip) {
            Write-Host "$name.$ZoneName -> $ip (exists)"
            continue
        }
        Write-Warning "$name.$ZoneName points to $($existing.RecordData.IPv4Address -join ', '); replacing with $ip"
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -Force
    }

    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $ip
    Write-Host "$name.$ZoneName -> $ip"
}
