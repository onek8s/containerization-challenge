<#
.SYNOPSIS
    Installs or updates one Order Hub Windows service (Topshelf host) from the publish folder.
.EXAMPLE
    .\Install-HubService.ps1 -Component Fulfilment -PublishPath C:\Build\OrderHub -ServiceAccount .\svc-orderhub-ful `
        -ServicePassword (Read-Host -AsSecureString) -SqlServer sql01.corp.local -SqlPassword (Read-Host -AsSecureString) `
        -RabbitMqPassword (Read-Host -AsSecureString) -Settings @{ 'Warehouse.Sftp.HostKeyFingerprint' = 'aa:bb:...' }
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Intake', 'Fulfilment', 'Partners', 'ShopSync')] [string] $Component,
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $InstallRoot = 'C:\OrderHub',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [string] $SqlServer = 'sql01.corp.local',
    [string] $DatabaseName = 'OrderHub',
    [securestring] $SqlPassword,
    [string] $RabbitMqHost = 'rabbitmq://mq.corp.local/orderhub',
    [Parameter(Mandatory)] [securestring] $RabbitMqPassword,
    [hashtable] $Settings = @{},
    [switch] $CreateLocalAccount,
    [switch] $NoStart
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Administrator

$serviceName = "OrderHub$Component"
$exeName = "OrderHub.$Component.exe"
$source = Join-Path $PublishPath $Component
$target = Join-Path $InstallRoot $Component
if (-not (Test-Path (Join-Path $source $exeName))) { throw "$exeName not found in $source. Run Publish.ps1 first." }

if ($CreateLocalAccount) {
    $localName = $ServiceAccount -replace '^\.\\', ''
    New-LocalServiceAccount -Name $localName -Password $ServicePassword -Description "Order Hub $Component service"
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Stop-Service $serviceName -Force
    (Get-Service $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
}

# Keep the installed configuration on update; only binaries and templates are replaced.
$configName = "$exeName.config"
$installedConfig = Join-Path $target $configName
$keepConfig = Test-Path $installedConfig
if ($keepConfig) { Copy-Item $installedConfig "$installedConfig.bak" -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Get-ChildItem $source | Where-Object { -not ($keepConfig -and $_.Name -eq $configName) } |
    Copy-Item -Destination $target -Recurse -Force

$values = @{
    'RabbitMq.Host'     = $RabbitMqHost
    'RabbitMq.Password' = (ConvertTo-PlainText $RabbitMqPassword)
    'serilog:write-to:File.path' = (Join-Path $InstallRoot "Logs\$($Component.ToLowerInvariant())-.log")
}
foreach ($key in $Settings.Keys) { $values[$key] = $Settings[$key] }
Set-AppSettings -ConfigPath $installedConfig -Settings $values

if ($Component -ne 'Intake') {
    if (-not $SqlPassword) { throw "-SqlPassword is required for $Component." }
    Set-ConnectionString -ConfigPath $installedConfig -Name 'OrderHub' -Value (
        New-SqlConnectionString $SqlServer $DatabaseName 'orderhub_svc' (ConvertTo-PlainText $SqlPassword) "OrderHub.$Component")
}

# Folders the service writes to.
Grant-FolderAccess -Path (Join-Path $InstallRoot 'Logs') -Account $ServiceAccount
Grant-FolderAccess -Path $target -Account $ServiceAccount -Rights 'ReadAndExecute'
$keys = Join-Path $InstallRoot 'Keys'
New-Item -ItemType Directory -Force -Path $keys | Out-Null
Grant-FolderAccess -Path $keys -Account $ServiceAccount -Rights 'Read'
if ($Component -eq 'Partners') {
    Grant-FolderAccess -Path (Join-Path $InstallRoot 'Partner') -Account $ServiceAccount
}

if ($Component -eq 'Intake') {
    [xml] $cfg = Get-Content $installedConfig -Raw
    $url = ($cfg.configuration.appSettings.add | Where-Object { $_.key -eq 'Intake.ListenUrl' }).value
    $port = ([uri] ($url -replace '\+', 'localhost')).Port
    netsh.exe http delete urlacl url=$url 2>$null | Out-Null
    netsh.exe http add urlacl url=$url user=$ServiceAccount | Out-Host
    if (-not (Get-NetFirewallRule -Name "OrderHub-Intake-$port" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name "OrderHub-Intake-$port" -DisplayName "Order Hub webhooks $port" -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
    }
}

Grant-ServiceLogonRight $ServiceAccount

$exe = Join-Path $target $exeName
if ($existing) { & $exe uninstall | Out-Host }
& $exe install -username:$ServiceAccount -password:(ConvertTo-PlainText $ServicePassword) --delayed | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Installing $serviceName failed." }

if (-not $NoStart) {
    Start-Service $serviceName
    Write-Host "$serviceName started. Log: $(Join-Path $InstallRoot 'Logs')"
}
