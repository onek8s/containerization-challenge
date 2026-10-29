<#
.SYNOPSIS
    Registers the Credit Desk host names in the DNS zone.

.DESCRIPTION
    Creates or updates A records for the web site, credit server, document server, database
    server, file server and print server. Every name defaults to -ServerAddress, so a test or
    lab installation can run all roles on one machine; pass the per-role parameters when the
    roles are on separate servers.

.EXAMPLE
    .\register-dns.ps1 -ZoneName fabrikam.local -ServerAddress 10.20.0.15

.EXAMPLE
    .\register-dns.ps1 -ZoneName fabrikam.local -ServerAddress 10.20.0.15 -CreditAddress 10.20.0.21 -DocsAddress 10.20.0.22
#>
[CmdletBinding()]
param(
    [string] $ZoneName = 'fabrikam.local',
    [Parameter(Mandatory)] [string] $ServerAddress,
    [string] $DnsServer = 'localhost',
    [string] $WebAddress = $ServerAddress,
    [string] $CreditAddress = $ServerAddress,
    [string] $DocsAddress = $ServerAddress,
    [string] $SqlAddress = $ServerAddress,
    [string] $FileAddress = $ServerAddress,
    [string] $PrintAddress = $ServerAddress
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not (Get-DnsServerZone -Name $ZoneName -ComputerName $DnsServer -ErrorAction SilentlyContinue)) {
    Write-Host "Creating primary zone $ZoneName"
    Add-DnsServerPrimaryZone -Name $ZoneName -ZoneFile "$ZoneName.dns" -ComputerName $DnsServer
}

$records = [ordered]@{
    'creditdesk' = $WebAddress
    'credit01'   = $CreditAddress
    'docs01'     = $DocsAddress
    'sql01'      = $SqlAddress
    'fs01'       = $FileAddress
    'print01'    = $PrintAddress
}

foreach ($name in $records.Keys) {
    $address = $records[$name]
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ComputerName $DnsServer -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -eq $address) {
            Write-Host "$name.$ZoneName -> $address (unchanged)"
            continue
        }
        $existing | Remove-DnsServerResourceRecord -ZoneName $ZoneName -ComputerName $DnsServer -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $address -ComputerName $DnsServer
    Write-Host "$name.$ZoneName -> $address"
}

Clear-DnsClientCache
foreach ($name in $records.Keys) {
    Resolve-DnsName "$name.$ZoneName" -Type A -Server $DnsServer | Format-Table Name, IPAddress -AutoSize
}
