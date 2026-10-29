<#
.SYNOPSIS
    Installs Message Queuing and creates the private transactional queues of the hub on this server.
    Run on invoices-proc (Role Processor: inbound and poison queues) and on invoices-erp (Role ErpLink:
    posting queue). The gateway server needs Message Queuing too (Role Gateway) for its outgoing queue.
.EXAMPLE
    .\Install-Queues.ps1 -Role Processor -SenderAccounts CORP\svc-inv-gw -ReceiverAccount CORP\svc-inv-proc
    .\Install-Queues.ps1 -Role ErpLink -SenderAccounts CORP\svc-inv-proc -ReceiverAccount CORP\svc-inv-erp
    .\Install-Queues.ps1 -Role Gateway
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Gateway', 'Processor', 'ErpLink', 'All')] [string]$Role,
    [string[]]$SenderAccounts = @(),
    [string]$ReceiverAccount,
    [string[]]$AdminAccounts = @('BUILTIN\Administrators')
)

$ErrorActionPreference = 'Stop'

$feature = Get-WindowsFeature MSMQ-Server
if (-not $feature.Installed) {
    Install-WindowsFeature MSMQ-Server, MSMQ-Directory -IncludeManagementTools | Out-Null
    Write-Host 'Message Queuing installed.'
}
Import-Module MSMQ

$queues = switch ($Role) {
    'Gateway'   { @() }
    'Processor' { @('invoices-inbound', 'invoices-poison') }
    'ErpLink'   { @('invoices-posting') }
    'All'       { @('invoices-inbound', 'invoices-poison', 'invoices-posting') }
}

foreach ($name in $queues) {
    $queue = Get-MsmqQueue -Name $name -QueueType Private -ErrorAction SilentlyContinue
    if (-not $queue) {
        $queue = New-MsmqQueue -Name $name -QueueType Private -Transactional -Journaling:$false
        Write-Host "Created private`$\$name"
    }

    $queue | Set-MsmqQueue -QueueQuota 1048576 | Out-Null
    foreach ($a in $AdminAccounts) {
        $queue | Set-MsmqQueueACL -UserName $a -Allow FullControl | Out-Null
    }
    if ($ReceiverAccount) {
        foreach ($right in 'ReceiveMessage', 'PeekMessage', 'GetQueueProperties', 'WriteMessage') {
            $queue | Set-MsmqQueueACL -UserName $ReceiverAccount -Allow $right | Out-Null
        }
    }
    foreach ($a in $SenderAccounts) {
        foreach ($right in 'WriteMessage', 'GetQueueProperties', 'PeekMessage') {
            $queue | Set-MsmqQueueACL -UserName $a -Allow $right | Out-Null
        }
    }
}

# Remote senders reach private queues over TCP 1801 and RPC.
if (-not (Get-NetFirewallRule -DisplayName 'Message Queuing (TCP-In)' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Message Queuing (TCP-In)' -Direction Inbound -Protocol TCP -LocalPort 1801 -Action Allow | Out-Null
}
