<#
.SYNOPSIS
    Installs RabbitMQ on mq.<zone> and creates the orderhub virtual host and the component users.
.DESCRIPTION
    Erlang/OTP and RabbitMQ are installed from the given installers (tested with OTP 23.3 and RabbitMQ 3.8.19).
    Without installers the script only configures an existing installation.
.EXAMPLE
    $pw = @{ intake = 'x'; fulfilment = 'x'; partners = 'x'; shopsync = 'x'; portal = 'x' }
    .\Install-RabbitMq.ps1 -ErlangInstaller C:\Install\otp_win64_23.3.exe -RabbitMqInstaller C:\Install\rabbitmq-server-3.8.19.exe `
        -AdminPassword (Read-Host -AsSecureString) -ComponentPasswords $pw
#>
param(
    [string] $ErlangInstaller,
    [string] $RabbitMqInstaller,
    [string] $VirtualHost = 'orderhub',
    [string] $AdminUser = 'orderhub-admin',
    [Parameter(Mandatory)] [securestring] $AdminPassword,
    [Parameter(Mandatory)] [hashtable] $ComponentPasswords,
    [switch] $KeepGuest
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Administrator

if ($ErlangInstaller) {
    Write-Host 'Installing Erlang/OTP'
    Start-Process $ErlangInstaller -ArgumentList '/S' -Wait
}
$erlang = Get-ChildItem 'C:\Program Files\erl*' -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $erlang) { throw 'Erlang/OTP is not installed.' }
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erlang.FullName, 'Machine')
$env:ERLANG_HOME = $erlang.FullName

if ($RabbitMqInstaller) {
    Write-Host 'Installing RabbitMQ'
    Start-Process $RabbitMqInstaller -ArgumentList '/S' -Wait
}
$sbin = Get-ChildItem 'C:\Program Files\RabbitMQ Server\rabbitmq_server-*\sbin' -Directory | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $sbin) { throw 'RabbitMQ is not installed.' }

# The service runs as LocalSystem and keeps its Erlang cookie in the system profile; the CLI tools use the
# cookie of the current user. They must match or rabbitmqctl cannot reach the node.
$serviceCookie = 'C:\Windows\System32\config\systemprofile\.erlang.cookie'
for ($i = 0; $i -lt 30 -and -not (Test-Path $serviceCookie); $i++) { Start-Sleep 2 }
if (Test-Path $serviceCookie) {
    Copy-Item $serviceCookie (Join-Path $env:USERPROFILE '.erlang.cookie') -Force
}

$ctl = Join-Path $sbin.FullName 'rabbitmqctl.bat'
$plugins = Join-Path $sbin.FullName 'rabbitmq-plugins.bat'

& $plugins enable rabbitmq_management
& $ctl await_startup
if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ node did not start.' }

$vhosts = & $ctl list_vhosts --quiet
if ($vhosts -notcontains $VirtualHost) { & $ctl add_vhost $VirtualHost }

function Set-RabbitUser([string] $Name, [string] $Password, [string] $Tags) {
    $users = & $ctl list_users --quiet | ForEach-Object { ($_ -split "`t")[0] }
    if ($users -contains $Name) { & $ctl change_password $Name $Password | Out-Null }
    else { & $ctl add_user $Name $Password | Out-Null }
    & $ctl set_user_tags $Name $Tags | Out-Null
    # MassTransit declares its exchanges and queues at start-up, so every component needs configure rights.
    & $ctl set_permissions -p $VirtualHost $Name '.*' '.*' '.*' | Out-Null
    Write-Host "RabbitMQ user $Name"
}

Set-RabbitUser $AdminUser (ConvertTo-PlainText $AdminPassword) 'administrator'
foreach ($component in $ComponentPasswords.Keys) {
    Set-RabbitUser "orderhub-$component" ([string] $ComponentPasswords[$component]) ''
}

if (-not $KeepGuest) {
    $users = & $ctl list_users --quiet | ForEach-Object { ($_ -split "`t")[0] }
    if ($users -contains 'guest') { & $ctl delete_user guest | Out-Null }
}

foreach ($port in 5672, 15672) {
    if (-not (Get-NetFirewallRule -Name "OrderHub-RabbitMQ-$port" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name "OrderHub-RabbitMQ-$port" -DisplayName "RabbitMQ $port" -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
    }
}
Write-Host "RabbitMQ ready: amqp://$env:COMPUTERNAME:5672/$VirtualHost, management http://$env:COMPUTERNAME:15672/"
