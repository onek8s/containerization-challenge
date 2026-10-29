<#
.SYNOPSIS
  Creates the A records for the Consumer Loans host names (DNS Server role / RSAT DNS tools).
#>
param(
    [string] $Config = (Join-Path $PSScriptRoot 'environment.psd1')
)
Import-Module (Join-Path $PSScriptRoot 'Common.psm1') -Force
$cfg = Import-DeployConfig $Config
Import-Module DnsServer

foreach ($name in $cfg.Hosts.Keys) {
    $ip = $cfg.Hosts[$name]
    $existing = Get-DnsServerResourceRecord -ComputerName $cfg.DnsServer -ZoneName $cfg.DnsZone -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -contains $ip) { Write-Host "$name.$($cfg.DnsZone) -> $ip (exists)"; continue }
        Remove-DnsServerResourceRecord -ComputerName $cfg.DnsServer -ZoneName $cfg.DnsZone -Name $name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ComputerName $cfg.DnsServer -ZoneName $cfg.DnsZone -Name $name -IPv4Address $ip
    Write-Host "$name.$($cfg.DnsZone) -> $ip"
}
