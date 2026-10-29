<#
.SYNOPSIS
    Creates the Fabrikam Housing host names in the corp.local zone.
.DESCRIPTION
    Each application has its own DNS name so that the applications can be moved between web servers
    without changing configuration. By default every name points to the one IP address given; use
    -Records to place applications on different servers.
.EXAMPLE
    .\register-dns.ps1 -IPAddress 10.10.0.40
.EXAMPLE
    .\register-dns.ps1 -Records @{ identity = '10.20.0.11'; tenants = '10.20.0.11'; 'contractors-api' = '10.20.0.11'; housing = '10.10.0.40'; 'housing-jobs' = '10.10.0.41'; docs01 = '10.10.0.42' } -DnsServer dc01.corp.local
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $IPAddress,
    [hashtable] $Records,
    [string] $ZoneName = 'corp.local',
    [string] $DnsServer = 'localhost',
    [string[]] $Names = @('identity', 'tenants', 'housing', 'contractors-api', 'housing-jobs', 'docs01')
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

if (-not $Records) {
    if (-not $IPAddress) { throw 'Give -IPAddress or -Records.' }
    $Records = @{}
    foreach ($name in $Names) { $Records[$name] = $IPAddress }
}

foreach ($name in $Records.Keys) {
    $address = $Records[$name]
    $existing = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        $current = $existing.RecordData.IPv4Address.IPAddressToString
        if ($current -eq $address) {
            Write-Host "$name.$ZoneName already points to $address"
            continue
        }
        if ($PSCmdlet.ShouldProcess("$name.$ZoneName", "Replace A record $current with $address")) {
            $existing | Remove-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Force
        }
    }
    if ($PSCmdlet.ShouldProcess("$name.$ZoneName", "Add A record $address")) {
        Add-DnsServerResourceRecordA -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -IPv4Address $address
        Write-Host "Added $name.$ZoneName -> $address"
    }
}
