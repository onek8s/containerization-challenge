#Requires -Version 7.2
<#
.SYNOPSIS
  Creates the "retail" virtual host, one user per service and their permissions on the RabbitMQ cluster.
.DESCRIPTION
  Uses the management HTTP API (port 15671, TLS). Exchanges and queues are created by MassTransit when
  the services start. The web shop bus (orders from the e-commerce platform) is on Azure Service Bus and
  is not configured here.
#>
param(
    [Parameter(Mandatory)][string] $Environment,
    [pscredential] $AdminCredential = (Get-Credential -Message 'RabbitMQ administrator')
)
. "$PSScriptRoot\Common.ps1"
$envData = Import-RetailEnvironment $Environment
$api = "https://$($envData.RabbitMqHost):15671/api"
$vhost = [uri]::EscapeDataString($envData.RabbitMqVHost)

function Invoke-Mq([string] $Method, [string] $Path, $Body) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 5 } else { $null }
    Invoke-RestMethod -Method $Method -Uri "$api/$Path" -Credential $AdminCredential -Authentication Basic -ContentType 'application/json' -Body $json
}

# Quorum queues by default; the three nodes survive one node patching at a time.
Invoke-Mq PUT "vhosts/$vhost" @{ description = 'VanArsdel retail back office'; tags = 'retail'; default_queue_type = 'quorum' }

$users = 'retail-catalog', 'retail-pricing', 'retail-inventory', 'retail-allocation', 'retail-orders', 'retail-customers',
         'retail-returns', 'retail-storeops', 'retail-reporting', 'retail-erpconnector', 'retail-notifications', 'retail-posfeed'
foreach ($user in $users) {
    $password = Get-RetailSecret $envData "rabbitmq-$user"
    Invoke-Mq PUT "users/$user" @{ password = $password; tags = '' }
    Invoke-Mq PUT "permissions/$vhost/$user" @{ configure = '.*'; write = '.*'; read = '.*' }
    Write-Host "User $user ready"
}

# Dead letters older than 14 days are dropped (the _error queues are monitored in Seq alerts).
Invoke-Mq PUT "policies/$vhost/error-queues" @{ pattern = '_error$'; 'apply-to' = 'queues'; definition = @{ 'message-ttl' = 1209600000 } }
