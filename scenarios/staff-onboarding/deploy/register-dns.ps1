<#
.SYNOPSIS
    Registers the Staff Onboarding host names in the DNS zone.
.DESCRIPTION
    Run on the DNS server. In a lab all names can point to one server.
.EXAMPLE
    .\register-dns.ps1 -ZoneName corp.local -IPAddress 192.168.8.136
#>
param(
    [string] $ZoneName = 'corp.local',
    [Parameter(Mandatory)] [string] $IPAddress,
    [string] $PortalHost = 'onboarding',
    [string] $WorkflowHost = 'onboarding-flow',
    [string] $ProvisioningHost = 'idm01',
    [string] $FileHost = 'files01',
    [string] $SqlHost = 'sql01',
    [string] $SmtpHost = 'smtp',
    [string] $WorkflowIPAddress = $IPAddress,
    [string] $ProvisioningIPAddress = $IPAddress,
    [string] $FileIPAddress = $IPAddress,
    [string] $SqlIPAddress = $IPAddress,
    [string] $SmtpIPAddress = $IPAddress
)

$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = @(
    @{ Name = $PortalHost; IP = $IPAddress },
    @{ Name = $WorkflowHost; IP = $WorkflowIPAddress },
    @{ Name = $ProvisioningHost; IP = $ProvisioningIPAddress },
    @{ Name = $FileHost; IP = $FileIPAddress },
    @{ Name = $SqlHost; IP = $SqlIPAddress },
    @{ Name = $SmtpHost; IP = $SmtpIPAddress }
)

foreach ($r in $records) {
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $r.Name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $r.Name -IPv4Address $r.IP
    Write-Host "$($r.Name).$ZoneName -> $($r.IP)"
}
