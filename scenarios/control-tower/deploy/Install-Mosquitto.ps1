<#
.SYNOPSIS
    Installs the Mosquitto 2.0 MQTT broker that receives truck telematics on mqtt01.
.DESCRIPTION
    TLS listener on 8883 (telematics units on the mobile network) and 1883 on localhost only.
    Every telematics unit has its own user and may publish only below proseware/telematics/<unit>/.
    The Control Tower telematics service subscribes with the user ct-telematics.
.EXAMPLE
    .\Install-Mosquitto.ps1 -Installer .\mosquitto-2.0.15-install-windows-x64.exe -CertificateFile mqtt01.crt -KeyFile mqtt01.key -CaFile corp-ca.crt `
        -ServicePassword $svc -Units @{ 'tu-1001' = '..'; 'tu-1002' = '..' }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$CertificateFile,
    [Parameter(Mandatory)][string]$KeyFile,
    [string]$CaFile,
    [Parameter(Mandatory)][object]$ServicePassword,
    [hashtable]$Units = @{},
    [string]$InstallPath = "$env:ProgramFiles\mosquitto",
    [string]$DataPath = 'D:\Mosquitto'
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

Write-Step 'Mosquitto'
if (-not (Test-Path (Join-Path $InstallPath 'mosquitto.exe'))) {
    Start-Process -FilePath $Installer -ArgumentList '/S', "/D=$InstallPath" -Wait
}
$passwd = Join-Path $InstallPath 'mosquitto_passwd.exe'
New-Item -ItemType Directory -Path $DataPath, (Join-Path $DataPath 'certs'), (Join-Path $DataPath 'log') -Force | Out-Null

Copy-Item $CertificateFile (Join-Path $DataPath 'certs\server.crt') -Force
Copy-Item $KeyFile (Join-Path $DataPath 'certs\server.key') -Force
if ($CaFile) { Copy-Item $CaFile (Join-Path $DataPath 'certs\ca.crt') -Force }

Write-Step 'Users'
$passwordFile = Join-Path $DataPath 'passwords'
if (Test-Path $passwordFile) { Remove-Item $passwordFile }
New-Item -ItemType File -Path $passwordFile | Out-Null
& $passwd -b $passwordFile ct-telematics (ConvertTo-PlainText $ServicePassword)
foreach ($unit in $Units.Keys) {
    & $passwd -b $passwordFile $unit (ConvertTo-PlainText $Units[$unit])
}

$acl = @(
    '# Control Tower telematics service',
    'user ct-telematics',
    'topic read proseware/telematics/#',
    'topic write proseware/telematics/+/cmd/#',
    '',
    '# Telematics units: own topics only',
    'pattern write proseware/telematics/%u/position',
    'pattern write proseware/telematics/%u/reefer',
    'pattern write proseware/telematics/%u/status',
    'pattern read proseware/telematics/%u/cmd/#'
)
$acl | Set-Content (Join-Path $DataPath 'acl') -Encoding ASCII

$conf = @"
per_listener_settings false
persistence true
persistence_location $($DataPath -replace '\\','/')/
autosave_interval 60
max_queued_messages 10000
log_dest file $($DataPath -replace '\\','/')/log/mosquitto.log
log_type error
log_type warning
log_type notice
connection_messages true
allow_anonymous false
password_file $($passwordFile -replace '\\','/')
acl_file $((Join-Path $DataPath 'acl') -replace '\\','/')

listener 1883 127.0.0.1

listener 8883
certfile $((Join-Path $DataPath 'certs\server.crt') -replace '\\','/')
keyfile $((Join-Path $DataPath 'certs\server.key') -replace '\\','/')
tls_version tlsv1.2
"@
if ($CaFile) { $conf += "`ncafile $((Join-Path $DataPath 'certs\ca.crt') -replace '\\','/')`n" }
$conf | Set-Content (Join-Path $InstallPath 'mosquitto.conf') -Encoding ASCII

Write-Step 'Service'
if (-not (Get-Service mosquitto -ErrorAction SilentlyContinue)) {
    & (Join-Path $InstallPath 'mosquitto.exe') install
}
& sc.exe config mosquitto start= auto | Out-Null
Restart-Service mosquitto

if (-not (Get-NetFirewallRule -DisplayName 'MQTT TLS' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'MQTT TLS' -Direction Inbound -Protocol TCP -LocalPort 8883 -Action Allow | Out-Null
}
Write-Host ''
Write-Host "Mosquitto listening on 8883 with $($Units.Count) telematics unit(s)."
