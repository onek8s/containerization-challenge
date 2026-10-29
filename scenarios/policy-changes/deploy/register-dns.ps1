<#
.SYNOPSIS
    Registers the Policy Changes host names in the DNS zone.
.DESCRIPTION
    Run on the DNS server. In a lab all names can point to one server.
.EXAMPLE
    .\register-dns.ps1 -ZoneName corp.local -IPAddress 192.168.8.136
.EXAMPLE
    .\register-dns.ps1 -IPAddress 10.0.0.20 -DeskIPAddresses 10.0.0.31,10.0.0.32 -SqlIPAddress 10.0.0.40 -FileServerIPAddress 10.0.0.41
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $PortalHost = 'changes',
    [string] $ApiHost = 'changes-api',
    [string[]] $DeskHosts = @('desk01', 'desk02'),
    [string[]] $DeskIPAddresses = @(),
    [string] $SqlHost = 'sql01',
    [string] $SqlIPAddress = $IPAddress,
    [string] $FileServerHost = 'files01',
    [string] $FileServerIPAddress = $IPAddress
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = @(
    @{ Name = $PortalHost; IP = $IPAddress },
    @{ Name = $ApiHost; IP = $IPAddress },
    @{ Name = $SqlHost; IP = $SqlIPAddress },
    @{ Name = $FileServerHost; IP = $FileServerIPAddress }
)
for ($i = 0; $i -lt $DeskHosts.Count; $i++) {
    $ip = if ($i -lt $DeskIPAddresses.Count) { $DeskIPAddresses[$i] } else { $IPAddress }
    $records += @{ Name = $DeskHosts[$i]; IP = $ip }
}

foreach ($r in $records) {
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $r.Name -IPv4Address $r.IP
    Write-Host "$($r.Name).$ZoneName -> $($r.IP)"
}
