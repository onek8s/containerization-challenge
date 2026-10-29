#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs or updates the Shift Clock service on a gate PC.
.EXAMPLE
    .\install-service.ps1 -Terminal GATE-NORTH -PortName COM3
#>
param(
    [string]$Terminal = $env:COMPUTERNAME,
    [string]$PortName = "COM3",
    [string]$InstallDir = "$env:ProgramFiles\Northwind\ShiftClock",
    [string]$DataDir = "$env:ProgramData\Northwind\ShiftClock",
    [string]$SupervisorGroup = "NORTHWIND\Shift Supervisors",
    [string]$ViewerGroup = "NORTHWIND\Shift Leads"
)

$ErrorActionPreference = "Stop"
$serviceName = "ShiftClock"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Publishing service and console..."
dotnet publish "$root\src\ShiftClock.Service" -c Release -r win-x64 --self-contained false -o "$root\publish\service"
dotnet publish "$root\src\ShiftClock.Console" -c Release -r win-x64 --self-contained false -o "$root\publish\console"

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne "Stopped") {
    Stop-Service -Name $serviceName
}

New-Item -ItemType Directory -Force -Path $InstallDir, "$InstallDir\Console", $DataDir | Out-Null
Copy-Item "$root\publish\service\*" $InstallDir -Recurse -Force
Copy-Item "$root\publish\console\*" "$InstallDir\Console" -Recurse -Force

# Gate settings are kept next to the binaries so updates do not overwrite them.
$settings = @{
    ShiftClock = @{
        Terminal        = $Terminal
        PortName        = $PortName
        SupervisorGroup = $SupervisorGroup
        ViewerGroup     = $ViewerGroup
    }
}
$settings | ConvertTo-Json -Depth 3 | Set-Content "$InstallDir\appsettings.Production.json" -Encoding UTF8

if (-not (Test-Path "$DataDir\employees.csv")) {
    Copy-Item "$PSScriptRoot\employees.sample.csv" "$DataDir\employees.csv"
}

# Only the service and administrators may touch the punch files.
icacls $DataDir /inheritance:r /grant:r "SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F" | Out-Null

if (-not [System.Diagnostics.EventLog]::SourceExists("Shift Clock")) {
    New-EventLog -LogName Application -Source "Shift Clock"
}

$binPath = "`"$InstallDir\ShiftClock.Service.exe`""
if (-not $existing) {
    sc.exe create $serviceName binPath= $binPath start= auto obj= LocalSystem DisplayName= "Northwind Shift Clock" | Out-Null
    sc.exe description $serviceName "Records employee clock-in and clock-out punches from the gate badge reader." | Out-Null
    sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}
else {
    sc.exe config $serviceName binPath= $binPath | Out-Null
}

[Environment]::SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production", "Machine")
Start-Service -Name $serviceName

$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:Public\Desktop\Shift Clock Console.lnk")
$shortcut.TargetPath = "$InstallDir\Console\ShiftClockConsole.exe"
$shortcut.Save()

Get-Service -Name $serviceName
