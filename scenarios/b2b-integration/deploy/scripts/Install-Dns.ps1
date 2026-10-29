<#
    CNAME records of the B2B host names. The public names (AS2, and b2b for the reverse proxy that publishes
    /partners/ and /tracking/) point at the load balancer VIP record of the zone; the operations portal is
    internal only. DEV uses localhost and has no records.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

if ($Environment -eq 'DEV' -or -not $Settings.DnsServer) {
    Write-B2BLog 'No DNS records for this environment.'
    return
}

$records = @(
    @{ Name = $Settings.As2PublicHost; Target = 'b2b-vip.' + $Settings.DnsZone;                      Zone = $Settings.DnsZone },
    @{ Name = $Settings.B2BPublicHost; Target = 'b2b-vip.' + $Settings.DnsZone;                      Zone = $Settings.DnsZone },
    @{ Name = $Settings.OpsPortalHost; Target = $Settings.WebServer + '.' + $Settings.DnsInternalZone; Zone = $Settings.DnsInternalZone }
)

Invoke-Command -ComputerName $Settings.DnsServer -ArgumentList (, $records) -ScriptBlock {
    param($records)
    Import-Module DnsServer
    foreach ($r in $records) {
        $relative = $r.Name -replace ('\.' + [regex]::Escape($r.Zone) + '$'), ''
        $existing = Get-DnsServerResourceRecord -ZoneName $r.Zone -Name $relative -RRType CName -ErrorAction SilentlyContinue
        if ($existing -and $existing.RecordData.HostNameAlias.TrimEnd('.') -ieq $r.Target) {
            "unchanged $($r.Name) -> $($r.Target)"
            continue
        }
        if ($existing) { Remove-DnsServerResourceRecord -ZoneName $r.Zone -InputObject $existing -Force }
        Add-DnsServerResourceRecordCName -ZoneName $r.Zone -Name $relative -HostNameAlias $r.Target -TimeToLive ([TimeSpan]::FromMinutes(15))
        "added $($r.Name) -> $($r.Target)"
    }
} | ForEach-Object { Write-B2BLog $_ }
