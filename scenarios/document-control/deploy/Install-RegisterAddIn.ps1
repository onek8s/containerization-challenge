<#
.SYNOPSIS
    Installs the transmittal register COM component on a user workstation for the Excel macros.
    Run as administrator. Registers the component for both 32-bit and 64-bit Office.
.EXAMPLE
    .\Install-RegisterAddIn.ps1 -PublishPath \\files01.corp.local\Software$\DocControl\Register
.EXAMPLE
    .\Install-RegisterAddIn.ps1 -PublishPath .\Register -Uninstall
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [string]$InstallPath = 'C:\Program Files\Litware\DocControl\Register',
    [string]$PortalUrl = 'https://doccontrol.corp.local',
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$dll = Join-Path $InstallPath 'Litware.DocControl.Register.dll'
$regasm = @(
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe",
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
) | Where-Object { Test-Path $_ }

if ($Uninstall) {
    foreach ($exe in $regasm) { & $exe /unregister $dll /silent }
    Remove-Item $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
    return
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Force
foreach ($exe in $regasm) {
    & $exe $dll /codebase /tlb /silent
    if ($LASTEXITCODE -ne 0) { throw "$exe failed ($LASTEXITCODE)." }
}

# Default portal address for all users; users can override it in HKCU.
New-Item -Path 'HKLM:\SOFTWARE\Policies\Litware\DocControl\Register' -Force | Out-Null
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Litware\DocControl\Register' -Name BaseUrl -Value $PortalUrl
Write-Host "Registered Litware.DocControl.RegisterClient from $dll"
