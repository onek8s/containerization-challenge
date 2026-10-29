<#
.SYNOPSIS
    Creates the DNS records for Lucerne Project Sites: the SharePoint web application (load balanced over the
    front ends), the add-in host, the app domain wildcard and the back-end services.
.EXAMPLE
    .\Register-DnsRecords.ps1 -FarmIP 10.40.2.10 -DashboardsIP 10.40.2.30 -SyncIP 10.40.2.40 -SqlIP 10.40.3.10
.EXAMPLE
    .\Register-DnsRecords.ps1 -AllIP 10.40.0.15     # single-server test environment
#>
[CmdletBinding()]
param(
    [string] $ZoneName = 'corp.local',
    [string] $DnsServer,
    [string] $AllIP,
    # Virtual IP of the load balancer in front of sp16-wfe01/sp16-wfe02.
    [string] $FarmIP,
    [string] $DashboardsIP,
    [string] $SyncIP,
    [string] $SqlIP,
    [string] $ErpIP,
    [string] $HrIP,
    [string] $DocControlIP,
    [string] $SmtpIP,
    # App domain for SharePoint-hosted add-ins (the app catalog needs it even though this add-in is provider hosted).
    [string] $AppDomainZone = 'apps.corp.local',
    [int] $TtlMinutes = 60
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Import-Module DnsServer
$settings = Get-ProjectSitesSettings
if (-not $DnsServer) { $DnsServer = $settings.DnsServer }

function Pick([string] $specific) { if ($specific) { $specific } elseif ($AllIP) { $AllIP } else { $null } }

$records = [ordered]@{
    'projects'   = Pick $FarmIP
    'dashboards' = Pick $DashboardsIP
    'sync01'     = Pick $SyncIP
    'sql01'      = Pick $SqlIP
    'spsql'      = Pick $SqlIP
    'erp01'      = Pick $ErpIP
    'hr01'       = Pick $HrIP
    'doccontrol' = Pick $DocControlIP
    'smtp'       = Pick $SmtpIP
}

$ttl = New-TimeSpan -Minutes $TtlMinutes
foreach ($name in $records.Keys) {
    $ip = $records[$name]
    if (-not $ip) { Write-Verbose "Skipping $name (no address)"; continue }
    $existing = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -eq $ip) { Write-Step "$name.$ZoneName already $ip"; continue }
        Remove-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ComputerName $DnsServer -ZoneName $ZoneName -Name $name -IPv4Address $ip -TimeToLive $ttl
    Write-Step "$name.$ZoneName -> $ip"
}

$farm = Pick $FarmIP
if ($farm) {
    if (-not (Get-DnsServerZone -ComputerName $DnsServer -Name $AppDomainZone -ErrorAction SilentlyContinue)) {
        Add-DnsServerPrimaryZone -ComputerName $DnsServer -Name $AppDomainZone -ReplicationScope Domain
    }
    if (-not (Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $AppDomainZone -Name '*' -RRType A -ErrorAction SilentlyContinue)) {
        Add-DnsServerResourceRecordA -ComputerName $DnsServer -ZoneName $AppDomainZone -Name '*' -IPv4Address $farm -TimeToLive $ttl
        Write-Step "*.$AppDomainZone -> $farm"
    }
}
