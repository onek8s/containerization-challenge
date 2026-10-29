<#
.SYNOPSIS
    Creates the A records of the Control Tower servers on the DNS server.
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 10.20.0.15
.EXAMPLE
    .\Install-DnsRecords.ps1 -ZoneName corp.local -WebIP 10.20.1.10 -AppIP 10.20.1.20 -SqlIP 10.20.1.30 -MqIP 10.20.1.40 -PrinterIPs @{ 'zt-vantaa-1' = '10.31.0.51' }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ZoneName,
    [string]$DnsServer = 'localhost',
    # One address for every name (single-server lab).
    [string]$AllIP,
    # Web front end (login, booking, track, tower, scan).
    [string]$WebIP,
    [string]$LoginIP, [string]$BookingIP, [string]$TrackIP, [string]$TowerIP, [string]$ScanIP,
    # Application servers.
    [string]$AppIP,
    [string]$Integ01IP, [string]$Events01IP, [string]$Jobs01IP,
    [string]$SqlIP, [string]$MqIP, [string]$MqttIP, [string]$SmtpIP,
    # Label printers by name; defaults to AllIP when not given.
    [hashtable]$PrinterIPs = @{},
    [int]$TtlMinutes = 60
)

. "$PSScriptRoot\common.ps1"
Import-Module DnsServer

function Resolve-Address([string]$specific, [string]$group) {
    if ($specific) { return $specific }
    if ($group) { return $group }
    if ($AllIP) { return $AllIP }
    return $null
}

$records = [ordered]@{
    'login'    = Resolve-Address $LoginIP $WebIP
    'booking'  = Resolve-Address $BookingIP $WebIP
    'track'    = Resolve-Address $TrackIP $WebIP
    'tower'    = Resolve-Address $TowerIP $WebIP
    'scan'     = Resolve-Address $ScanIP $WebIP
    'integ01'  = Resolve-Address $Integ01IP $AppIP
    'events01' = Resolve-Address $Events01IP $AppIP
    'jobs01'   = Resolve-Address $Jobs01IP $AppIP
    'sql01'    = Resolve-Address $SqlIP $null
    'mq01'     = Resolve-Address $MqIP $null
    'mqtt01'   = Resolve-Address $MqttIP $MqIP
    'smtp'     = Resolve-Address $SmtpIP $null
}
foreach ($printer in 'zt-vantaa-1', 'zt-vantaa-2', 'zt-turku-1', 'zt-gothenburg-1') {
    $records[$printer] = if ($PrinterIPs.ContainsKey($printer)) { $PrinterIPs[$printer] } else { $AllIP }
}

if (-not (Get-DnsServerZone -Name $ZoneName -ComputerName $DnsServer -ErrorAction SilentlyContinue)) {
    throw "Zone $ZoneName does not exist on $DnsServer."
}

Write-Step "A records in $ZoneName on $DnsServer"
$ttl = New-TimeSpan -Minutes $TtlMinutes
foreach ($name in $records.Keys) {
    $ip = $records[$name]
    if (-not $ip) {
        Write-Warning "No address for $name.$ZoneName, skipped."
        continue
    }
    $existing = Get-DnsServerResourceRecord -ZoneName $ZoneName -Name $name -RRType A -ComputerName $DnsServer -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.RecordData.IPv4Address.IPAddressToString -contains $ip) {
            Write-Host "  $name -> $ip (exists)"
            continue
        }
        $existing | Remove-DnsServerResourceRecord -ZoneName $ZoneName -ComputerName $DnsServer -Force
    }
    Add-DnsServerResourceRecordA -ZoneName $ZoneName -Name $name -IPv4Address $ip -TimeToLive $ttl -ComputerName $DnsServer
    Write-Host "  $name -> $ip"
}
