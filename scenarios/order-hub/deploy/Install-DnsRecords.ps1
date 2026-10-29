<#
.SYNOPSIS
    Registers the Order Hub host names in the DNS zone (run on the DNS server).
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 10.0.0.15
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -HubIP 10.0.1.20 -IntakeIP 10.0.1.21 -FulfilmentIP 10.0.1.22 -PartnersIP 10.0.1.23 `
        -ShopSyncIP 10.0.1.24 -MqIP 10.0.1.30 -SftpIP 10.0.2.10 -ReportsIP 10.0.1.40 -SqlIP 10.0.1.50 -SmtpIP 10.0.1.60
#>
param(
    [Parameter(Mandatory)] [string] $ZoneName,
    [string] $AllIP,
    [string] $HubIP, [string] $IntakeIP, [string] $FulfilmentIP, [string] $PartnersIP, [string] $ShopSyncIP,
    [string] $MqIP, [string] $SftpIP, [string] $ReportsIP, [string] $SqlIP, [string] $SmtpIP
)
$ErrorActionPreference = 'Stop'
Import-Module DnsServer

$records = [ordered]@{
    'hub'      = $HubIP
    'intake'   = $IntakeIP
    'fulfil'   = $FulfilmentIP
    'partners' = $PartnersIP
    'shopsync' = $ShopSyncIP
    'mq'       = $MqIP
    'sftp'     = $SftpIP
    'reports'  = $ReportsIP
    'sql01'    = $SqlIP
    'smtp'     = $SmtpIP
}

foreach ($name in $records.Keys) {
    $ip = if ($records[$name]) { $records[$name] } else { $AllIP }
    if (-not $ip) { Write-Warning "No address for $name.$ZoneName, skipped"; continue }

    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -eq $ip) { Write-Host "$name.$ZoneName -> $ip (exists)"; continue }
        Remove-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $ip
    Write-Host "$name.$ZoneName -> $ip"
}
