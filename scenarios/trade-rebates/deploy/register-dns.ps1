<#
.SYNOPSIS
    Creates the Trade Rebates host names in the corp.local zone.
.EXAMPLE
    .\register-dns.ps1 -IPAddress 10.10.0.21
    .\register-dns.ps1 -IPAddress 10.10.0.21 -ZoneName corp.local -DnsServer dc01.corp.local
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $ZoneName = 'corp.local',
    [string] $DnsServer = 'localhost',
    [string[]] $Names = @('rebates', 'rebates-api', 'rebates-sql', 'rebates-files')
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

foreach ($name in $Names) {
    $existing = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        $current = $existing.RecordData.IPv4Address.IPAddressToString
        if ($current -eq $IPAddress) {
            Write-Host "$name.$ZoneName already points to $IPAddress"
            continue
        }
        if ($PSCmdlet.ShouldProcess("$name.$ZoneName", "Replace A record $current with $IPAddress")) {
            $existing | Remove-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Force
        }
    }
    if ($PSCmdlet.ShouldProcess("$name.$ZoneName", "Add A record $IPAddress")) {
        Add-DnsServerResourceRecordA -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -IPv4Address $IPAddress
        Write-Host "Added $name.$ZoneName -> $IPAddress"
    }
}
