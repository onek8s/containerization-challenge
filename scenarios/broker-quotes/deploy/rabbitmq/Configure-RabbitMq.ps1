<#
.SYNOPSIS
    Creates the RabbitMQ virtual host and users for Broker Quotes.
.DESCRIPTION
    Run on mq01 after installing Erlang/OTP 25 and RabbitMQ 3.12 (Windows installer, service
    "RabbitMQ"). The applications create their exchanges and queues when they start.
.EXAMPLE
    .\Configure-RabbitMq.ps1 -Passwords @{ 'quote-api' = '...'; 'policy-service' = '...'; 'documents' = '...' }
#>
param(
    [Parameter(Mandatory)] [hashtable] $Passwords,
    [string] $VirtualHost = 'humongous',
    [string] $RabbitMqSbin = 'C:\Program Files\RabbitMQ Server\rabbitmq_server-3.12.1\sbin'
)

$ErrorActionPreference = 'Stop'
$ctl = Join-Path $RabbitMqSbin 'rabbitmqctl.bat'
if (-not (Test-Path $ctl)) { throw "rabbitmqctl not found in $RabbitMqSbin." }

$vhosts = & $ctl list_vhosts --quiet
if ($vhosts -notcontains $VirtualHost) { & $ctl add_vhost $VirtualHost }

foreach ($user in $Passwords.Keys) {
    $users = & $ctl list_users --quiet | ForEach-Object { ($_ -split "`t")[0] }
    if ($users -contains $user) { & $ctl change_password $user $Passwords[$user] }
    else { & $ctl add_user $user $Passwords[$user] }
    & $ctl set_permissions -p $VirtualHost $user '.*' '.*' '.*'
    Write-Host "User $user on vhost $VirtualHost"
}

& (Join-Path $RabbitMqSbin 'rabbitmq-plugins.bat') enable rabbitmq_management
