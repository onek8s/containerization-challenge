<#
.SYNOPSIS
    Creates the A records for the clinic host names on the corp.local DNS server (dnscmd, run as a DNS administrator).
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production'
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings = Get-DeploySettings $Environment $null

Write-Step "DNS records in $($settings.DnsZone) on $($settings.DnsServer)"
foreach ($name in $settings.Hosts.Keys | Sort-Object) {
    $address = $settings.Hosts[$name]
    & dnscmd.exe $settings.DnsServer /RecordDelete $settings.DnsZone $name A /f 2>$null | Out-Null
    & dnscmd.exe $settings.DnsServer /RecordAdd $settings.DnsZone $name A $address | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dnscmd could not add $name.$($settings.DnsZone)." }
    Write-Host ("    {0,-12} {1}" -f $name, $address)
}
