<#
.SYNOPSIS
    Installs Erlang and RabbitMQ on mq01 and creates the controltower virtual host and users.
.EXAMPLE
    $pw = @{ 'ct-booking'='..'; 'ct-portal'='..'; 'ct-tower'='..'; 'ct-scan'='..'; 'ct-gateway'='..'; 'ct-events'='..'; 'ct-telematics'='..'; 'ct-jobs'='..' }
    .\Install-RabbitMq.ps1 -ErlangInstaller .\otp_win64_24.3.4.exe -RabbitMqInstaller .\rabbitmq-server-3.9.29.exe -AdminPassword $admin -ComponentPasswords $pw
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ErlangInstaller,
    [Parameter(Mandatory)][string]$RabbitMqInstaller,
    [Parameter(Mandatory)][object]$AdminPassword,
    [Parameter(Mandatory)][hashtable]$ComponentPasswords,
    [string]$VirtualHost = 'controltower',
    [string]$DataFolder = 'D:\RabbitMQ'
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

Write-Step 'Erlang'
if (-not (Get-ChildItem "$env:ProgramFiles\Erl*" -Directory -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $ErlangInstaller -ArgumentList '/S' -Wait
}
$erl = Get-ChildItem "$env:ProgramFiles\Erl*" -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $erl) { throw 'Erlang installation not found.' }
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erl.FullName, 'Machine')
$env:ERLANG_HOME = $erl.FullName

Write-Step 'RabbitMQ'
New-Item -ItemType Directory -Path $DataFolder -Force | Out-Null
[Environment]::SetEnvironmentVariable('RABBITMQ_BASE', $DataFolder, 'Machine')
$env:RABBITMQ_BASE = $DataFolder
if (-not (Get-Service RabbitMQ -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $RabbitMqInstaller -ArgumentList '/S' -Wait
}
$sbin = Get-ChildItem "$env:ProgramFiles\RabbitMQ Server\rabbitmq_server-*\sbin" -Directory | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $sbin) { throw 'RabbitMQ installation not found.' }
$ctl = Join-Path $sbin.FullName 'rabbitmqctl.bat'
$plugins = Join-Path $sbin.FullName 'rabbitmq-plugins.bat'

# Erlang cookie of the service account must also be in the installing user's profile for rabbitmqctl.
$cookie = Join-Path $env:SystemRoot 'System32\config\systemprofile\.erlang.cookie'
if (Test-Path $cookie) { Copy-Item $cookie (Join-Path $env:USERPROFILE '.erlang.cookie') -Force }

& $plugins enable rabbitmq_management | Out-Null
Restart-Service RabbitMQ
Start-Sleep -Seconds 10
& $ctl await_startup | Out-Null

Write-Step "Virtual host $VirtualHost and users"
& $ctl add_vhost $VirtualHost 2>$null | Out-Null
& $ctl add_user ct-admin (ConvertTo-PlainText $AdminPassword) 2>$null | Out-Null
& $ctl change_password ct-admin (ConvertTo-PlainText $AdminPassword) | Out-Null
& $ctl set_user_tags ct-admin administrator | Out-Null
& $ctl set_permissions -p $VirtualHost ct-admin '.*' '.*' '.*' | Out-Null

foreach ($user in $ComponentPasswords.Keys) {
    & $ctl add_user $user (ConvertTo-PlainText $ComponentPasswords[$user]) 2>$null | Out-Null
    & $ctl change_password $user (ConvertTo-PlainText $ComponentPasswords[$user]) | Out-Null
    # The applications declare their own exchanges and queues (EasyNetQ conventions).
    & $ctl set_permissions -p $VirtualHost $user '.*' '.*' '.*' | Out-Null
    Write-Host "  $user"
}
& $ctl delete_user guest 2>$null | Out-Null

if (-not (Get-NetFirewallRule -DisplayName 'RabbitMQ AMQP' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'RabbitMQ AMQP' -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow -Profile Domain, Private | Out-Null
    New-NetFirewallRule -DisplayName 'RabbitMQ management' -Direction Inbound -Protocol TCP -LocalPort 15672 -Action Allow -Profile Domain, Private | Out-Null
}
Write-Host ''
Write-Host 'Management UI: http://mq01:15672/ (user ct-admin)'
