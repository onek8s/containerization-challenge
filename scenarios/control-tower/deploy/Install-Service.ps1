<#
.SYNOPSIS
    Installs one Control Tower background service as a Windows service.
.EXAMPLE
    .\Install-Service.ps1 -Component Events -PublishPath C:\Build\ControlTower -ServiceAccount .\svc-ct-events -ServicePassword $p `
        -SqlPassword $eventsSql -RabbitMqPassword $eventsMq -CreateLocalAccount
.EXAMPLE
    .\Install-Service.ps1 -Component Jobs -PublishPath C:\Build\ControlTower -ServiceAccount CORP\svc-ct-jobs -ServicePassword $p `
        -SqlPassword $jobsSql -RabbitMqPassword $jobsMq -Settings @{ 'Jobs:Dashboard:Password' = '...' }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('CarrierGateway', 'Telematics', 'Events', 'Jobs')][string]$Component,
    [Parameter(Mandatory)][string]$PublishPath,
    [string]$ZoneName = 'corp.local',
    [Parameter(Mandatory)][string]$ServiceAccount,
    [Parameter(Mandatory)][securestring]$ServicePassword,
    [switch]$CreateLocalAccount,
    [string]$SqlServer,
    [Parameter(Mandatory)][object]$SqlPassword,
    [string]$RabbitMqHost,
    [Parameter(Mandatory)][object]$RabbitMqPassword,
    [hashtable]$Settings = @{},
    [string]$InstallRoot = 'C:\Program Files\Proseware\ControlTower'
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

$def = $Services[$Component]
if (-not $SqlServer) { $SqlServer = "sql01.$ZoneName" }
if (-not $RabbitMqHost) { $RabbitMqHost = "mq01.$ZoneName" }
$source = Join-Path $PublishPath $def.Project
$target = Join-Path $InstallRoot $Component
$major = if ($Component -in 'Telematics', 'Jobs') { 7 } else { 6 }

if (-not (Test-Path (Join-Path $source $def.Exe))) { throw "$($def.Exe) not found in $source. Run Publish.ps1 first." }
if (-not (Test-DotNetRuntime -MajorVersion $major)) { throw ".NET $major runtime is not installed." }
if ($Component -eq 'Jobs' -and -not (Get-ChildItem "$env:ProgramFiles\dotnet\shared\Microsoft.AspNetCore.App" -Directory -ErrorAction SilentlyContinue | Where-Object Name -like '7.*')) {
    throw 'ASP.NET Core 7 runtime is required by the jobs service.'
}

if ($CreateLocalAccount) {
    $name = ($ServiceAccount -split '\\')[-1]
    Write-Step "Local account $name"
    $ServiceAccount = New-ServiceAccount -Name $name -Password $ServicePassword -Description "Control Tower $Component"
}

Write-Step 'Log on as a service'
Grant-UserRight -Account $ServiceAccount -Right 'SeServiceLogonRight'

Write-Step "Files to $target"
$existing = Get-Service -Name $def.ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Stop-Service -Name $def.ServiceName -Force
    (Get-Service $def.ServiceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
& robocopy.exe $source $target /MIR /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the files failed (robocopy $LASTEXITCODE)." }

Write-Step 'Folders'
$folders = @((Join-Path $ControlTowerRoot 'Logs'))
switch ($Component) {
    'CarrierGateway' { $folders += (Join-Path $ControlTowerRoot 'Edi'), (Join-Path $ControlTowerRoot 'Documents') }
    'Telematics'     { $folders += (Join-Path $ControlTowerRoot 'Telematics') }
    'Events'         { $folders += (Join-Path $ControlTowerRoot 'Routing') }
    'Jobs'           { $folders += (Join-Path $ControlTowerRoot 'Reports'), (Join-Path $ControlTowerRoot 'Export\Invoices'), (Join-Path $ControlTowerRoot 'Import\Fuel'), (Join-Path $ControlTowerRoot 'Documents') }
}
foreach ($f in $folders) { Grant-FolderAccess -Path $f -Identity $ServiceAccount -Rights Modify }
Grant-FolderAccess -Path $target -Identity $ServiceAccount -Rights ReadAndExecute

Write-Step 'Settings'
$values = @{
    'ConnectionStrings:ControlTower' = (New-SqlConnectionString -Server $SqlServer -Database 'ControlTower' -User $def.SqlLogin -Password $SqlPassword -ApplicationName "ControlTower.$Component")
    'ConnectionStrings:RabbitMq'     = (New-RabbitConnectionString -HostName $RabbitMqHost -User $def.MqUser -Password $RabbitMqPassword)
}
foreach ($k in $Settings.Keys) { $values[$k] = $Settings[$k] }
Set-AppSettings -Path (Join-Path $target 'appsettings.Production.json') -Values $values

Write-Step "Service $($def.ServiceName)"
$binPath = '"' + (Join-Path $target $def.Exe) + '"'
$plain = ConvertTo-PlainText $ServicePassword
if (-not $existing) {
    & sc.exe create $def.ServiceName binPath= $binPath start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= $def.DisplayName | Out-Null
} else {
    & sc.exe config $def.ServiceName binPath= $binPath start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= $def.DisplayName | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw "sc.exe failed ($LASTEXITCODE)." }
& sc.exe description $def.ServiceName "Proseware Control Tower: $Component" | Out-Null
# Restart after 30 s, 60 s and then every 5 minutes; reset the counter after a day.
& sc.exe failure $def.ServiceName reset= 86400 actions= restart/30000/restart/60000/restart/300000 | Out-Null
& sc.exe failureflag $def.ServiceName 1 | Out-Null
# Production environment for the generic host.
$envKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$($def.ServiceName)"
Set-ItemProperty -Path $envKey -Name Environment -Type MultiString -Value @('DOTNET_ENVIRONMENT=Production', 'ASPNETCORE_ENVIRONMENT=Production')

foreach ($port in $def.Ports) {
    $rule = "Control Tower $Component $port"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow -Profile Domain, Private | Out-Null
    }
}

Start-Service -Name $def.ServiceName
Write-Host ''
Write-Host "$($def.ServiceName) running as $ServiceAccount."
