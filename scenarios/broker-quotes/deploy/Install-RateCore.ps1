<#
.SYNOPSIS
    Installs a RateCore release (RateCore.dll and rate tables) on a server that rates quotes.
.DESCRIPTION
    RateCore is the pricing team's VB6 rating component (legacy\RateCore). Releases are built on
    the pricing build PC and published, with the signed-off Access rate tables, to the pricing
    release share:

        \\files01.corp.local\PricingRelease$\RateCore\<version>\
            RateCore.dll
            Tables\VERSIONS.mdb, BC2023.mdb, BC2024.mdb, ZONES.mdb
            SIGNOFF.txt

    Install it on the quote API server (quote-api) and on the application server (app01).
    The script copies the DLL, registers it with the 32-bit regsvr32, copies the tables and gives
    the application identities read access.

    Requirements on the server: Windows Server 2016 or later with the 32-bit VB6 runtime
    (msvbvm60.dll, part of Windows), DAO 3.6 / Jet 4.0 (dao360.dll, part of 32-bit Windows
    components) and the Scripting Runtime (scrrun.dll).
.EXAMPLE
    .\Install-RateCore.ps1 -ReleasePath \\files01.corp.local\PricingRelease$\RateCore\4.12.318 -ReadAccess 'IIS AppPool\quote-api'
.EXAMPLE
    .\Install-RateCore.ps1 -ReleasePath \\files01.corp.local\PricingRelease$\RateCore\4.12.318 -ReadAccess 'CORP\svc-hq-policy$'
#>
param(
    [Parameter(Mandatory)] [string] $ReleasePath,
    [string] $InstallPath = "${env:ProgramFiles(x86)}\Humongous\RateCore",
    [string] $TablesPath = 'D:\RateCore\Tables',
    [string[]] $ReadAccess = @(),
    [string] $TraceFile
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator

$regsvr32 = Join-Path $env:SystemRoot 'SysWOW64\regsvr32.exe'
$dao = "${env:CommonProgramFiles(x86)}\Microsoft Shared\DAO\dao360.dll"
foreach ($file in @($regsvr32, $dao, (Join-Path $env:SystemRoot 'SysWOW64\msvbvm60.dll'), (Join-Path $env:SystemRoot 'SysWOW64\scrrun.dll'))) {
    if (-not (Test-Path $file)) { throw "Missing prerequisite $file. RateCore needs the 32-bit Windows components." }
}

$dll = Join-Path $ReleasePath 'RateCore.dll'
if (-not (Test-Path $dll)) { throw "RateCore.dll not found in $ReleasePath." }
if (-not (Test-Path (Join-Path $ReleasePath 'SIGNOFF.txt'))) { throw "The release in $ReleasePath has not been signed off by Pricing." }

# Unregister the previous version first so its class registrations do not linger.
$installed = Join-Path $InstallPath 'RateCore.dll'
if (Test-Path $installed) {
    & $regsvr32 /s /u $installed
    Write-Host "Unregistered $((Get-Item $installed).VersionInfo.FileVersion)"
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Copy-Item $dll $InstallPath -Force
& $regsvr32 /s $installed
if ($LASTEXITCODE -ne 0) { throw "regsvr32 failed with $LASTEXITCODE." }
if (-not (Test-RateCoreRegistered)) { throw 'RateCore.Engine is not registered in the 32-bit registry view.' }
Write-Host "Registered RateCore $((Get-Item $installed).VersionInfo.FileVersion)"

New-Item -ItemType Directory -Path $TablesPath -Force | Out-Null
Copy-Item (Join-Path $ReleasePath 'Tables\*.mdb') $TablesPath -Force
foreach ($identity in $ReadAccess) {
    Grant-FolderAccess -Path $TablesPath -Identity $identity -Rights ReadAndExecute
    Grant-FolderAccess -Path $InstallPath -Identity $identity -Rights ReadAndExecute
}
Write-Host "Rate tables copied to $TablesPath"

if ($TraceFile) {
    $key = 'HKLM:\SOFTWARE\WOW6432Node\Humongous\RateCore'
    New-Item $key -Force | Out-Null
    Set-ItemProperty $key -Name TraceFile -Value $TraceFile
    Write-Host "RateCore trace file: $TraceFile"
}
