<#
.SYNOPSIS
    Installs the interface engine Windows service (installutil) and the billing and reminder scheduled tasks
    on clinic-int (after Publish-Applications.ps1).
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production',
    [string] $StagingRoot = (Join-Path $PSScriptRoot 'out')
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings = Get-DeploySettings $Environment $null
$staging = Join-Path $StagingRoot $settings.Name
$appsRoot = New-Folder (Get-LocalPath $settings.AppsRoot)
$dataRoot = New-Folder (Get-LocalPath $settings.DataRoot)
$installUtil = Join-Path $env:windir 'Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe'
$serviceName = $settings.InterfaceEngine.ServiceName

Write-Step "Interface engine service $serviceName"
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') { Stop-Service $serviceName; $existing.WaitForStatus('Stopped', '00:01:00') }
}
foreach ($name in 'InterfaceEngine', 'BillingBatch', 'Reminders') {
    $target = New-Folder (Join-Path $appsRoot $name)
    & robocopy.exe (Join-Path $staging "apps\$name") $target /MIR /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed (robocopy $LASTEXITCODE)." }
}
Grant-FolderAccess (Join-Path $appsRoot 'logs') 'NT AUTHORITY\LOCAL SERVICE'
Grant-FolderAccess (Join-Path $appsRoot 'mailpickup') 'NT AUTHORITY\LOCAL SERVICE'
$exe = Join-Path $appsRoot 'InterfaceEngine\SouthridgeInterfaceEngine.exe'
if (-not $existing) {
    & $installUtil /LogToConsole=false /LogFile= $exe
    if ($LASTEXITCODE -ne 0) { throw 'installutil failed.' }
}
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
Start-Service $serviceName

Write-Step 'Firewall rules for the MLLP listeners'
[xml] $engineConfig = Get-Content "$exe.config"
foreach ($listener in $engineConfig.configuration.interfaceEngine.listeners.add) {
    $rule = "Southridge HL7 $($listener.partner)"
    & netsh.exe advfirewall firewall delete rule name="$rule" | Out-Null
    & netsh.exe advfirewall firewall add rule name="$rule" dir=in action=allow protocol=TCP localport=$($listener.port) | Out-Null
    Write-Host "    $rule TCP $($listener.port)"
}

Write-Step 'Scheduled tasks'
Grant-FolderAccess $dataRoot 'NT AUTHORITY\NETWORK SERVICE'
foreach ($task in $settings.Tasks) {
    $program = Join-Path $appsRoot $task.Program
    $command = "`"$program`" $($task.Arguments)"
    $taskArgs = @('/Create', '/F', '/TN', $task.Name, '/TR', $command, '/SC', $task.Schedule, '/ST', $task.At, '/RU', 'NT AUTHORITY\NETWORK SERVICE', '/RL', 'LIMITED')
    if ($task.ContainsKey('Day')) { $taskArgs += @('/D', $task.Day) }
    & schtasks.exe @taskArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "schtasks could not create '$($task.Name)'." }
    Write-Host "    $($task.Name)  ($($task.Schedule) $($task.At))"
}
