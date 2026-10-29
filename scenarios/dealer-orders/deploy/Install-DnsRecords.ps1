<#
.SYNOPSIS
    A and CNAME records for Dealer Orders in the corp.local zone. Run on a domain controller (DnsServer module).
.EXAMPLE
    .\Install-DnsRecords.ps1 -WebServer 10.20.4.21 -AxIntegrationServer 10.20.4.31 -EdiServer 10.20.6.11 -AppServer 10.20.4.41
#>
[CmdletBinding()]
param(
    [string]$ZoneName = 'corp.local',
    [Parameter(Mandatory)][string]$WebServer,
    [Parameter(Mandatory)][string]$AxIntegrationServer,
    [Parameter(Mandatory)][string]$EdiServer,
    [Parameter(Mandatory)][string]$AppServer,
    [string]$SqlServer,
    [string]$AosServer,
    [string]$SmtpServer
)

. "$PSScriptRoot\common.ps1"
Import-Module DnsServer

function Set-ARecord([string]$Name, [string]$Address) {
    if (-not $Address) { return }
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $Name -RRType A -ErrorAction SilentlyContinue
    if ($existing) { Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $Name -RRType A -Force }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $Name -IPv4Address $Address -CreatePtr
    Write-Host "  $Name.$ZoneName -> $Address"
}

Write-Step 'Servers'
Set-ARecord 'web01' $WebServer
Set-ARecord 'axint01' $AxIntegrationServer
Set-ARecord 'edi01' $EdiServer
Set-ARecord 'app01' $AppServer
Set-ARecord 'sql01' $SqlServer
Set-ARecord 'aos01' $AosServer
Set-ARecord 'smtp' $SmtpServer

Write-Step 'Site names (IIS host headers on web01)'
foreach ($site in 'dealers', 'salesdesk', 'docs') {
    if (-not (Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $site -RRType CName -ErrorAction SilentlyContinue)) {
        Add-DnsServerResourceRecordCName -ZoneName $ZoneName -Name $site -HostNameAlias "web01.$ZoneName"
    }
    Write-Host "  $site.$ZoneName -> web01.$ZoneName"
}
# dealers.tailspin-machinery.example (public) is published through the reverse proxy in the DMZ to web01:443,
# and as2.tailspin-machinery.example to edi01:4080. Those records are in the external DNS, not here.
