#Requires -Version 5.1
<#
.SYNOPSIS
  Creates the A records of the app hosts and the CNAME records of the sites on the AD-integrated DNS zone.
.EXAMPLE
  .\Install-DnsRecords.ps1 -Environment .\lab.psd1
#>
param([Parameter(Mandatory)][string] $Environment)

$ErrorActionPreference = 'Stop'
$envData = Import-PowerShellDataFile -Path (Resolve-Path $Environment)
$zone = $envData.DnsZone
$server = $envData.DnsServer
Import-Module DnsServer

function Set-ARecord([string] $fqdn, [string] $address) {
    $name = $fqdn -replace "\.$([regex]::Escape($zone))$", ''
    $existing = Get-DnsServerResourceRecord -ComputerName $server -ZoneName $zone -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing -and $existing.RecordData.IPv4Address.IPAddressToString -eq $address) { return }
    if ($existing) { Remove-DnsServerResourceRecord -ComputerName $server -ZoneName $zone -InputObject $existing -Force }
    Add-DnsServerResourceRecordA -ComputerName $server -ZoneName $zone -Name $name -IPv4Address $address -CreatePtr
    Write-Host "A     $fqdn -> $address"
}

function Set-CName([string] $fqdn, [string] $target) {
    $name = $fqdn -replace "\.$([regex]::Escape($zone))$", ''
    $existing = Get-DnsServerResourceRecord -ComputerName $server -ZoneName $zone -Name $name -RRType CName -ErrorAction SilentlyContinue
    if ($existing -and $existing.RecordData.HostNameAlias.TrimEnd('.') -eq $target) { return }
    if ($existing) { Remove-DnsServerResourceRecord -ComputerName $server -ZoneName $zone -InputObject $existing -Force }
    Add-DnsServerResourceRecordCName -ComputerName $server -ZoneName $zone -Name $name -HostNameAlias $target
    Write-Host "CNAME $fqdn -> $target"
}

foreach ($h in $envData.Hosts.Values) { Set-ARecord $h.Name $h.Address }
foreach ($site in $envData.WebSites) { Set-CName $site.HostName $envData.Hosts[$site.Host].Name }
foreach ($svc in $envData.Services | Where-Object { $_.ContainsKey('HostName') }) { Set-CName $svc.HostName $envData.Hosts[$svc.Host].Name }
