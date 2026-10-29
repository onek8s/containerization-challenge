<#
.SYNOPSIS
    Prepares a server that runs the Geomatix engine in process (back office, map service, jobs).
.DESCRIPTION
    Prerequisites (installed by the GIS team, not by this script):
      - Geomatix Pro 3.2 (x64) with the CoreHost SDK feature, installed to C:\Program Files\Geomatix\Pro
      - Single Use licence (Standard) authorised for this machine with the Licence Administrator.
        The authorisation is bound to the machine fingerprint; deauthorise before rebuilding a server.
      - Replica registration PERMITS_<HOSTNAME> on the corporate geodatabase (gis-sql), created by the GIS team.
    The script copies the map document, layouts, basemap tile package and connection file, creates the
    first replica and grants the service account read access.
#>
param(
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [string] $InstallDir = 'C:\Program Files\Geomatix\Pro',
    [string] $GisRoot = 'D:\Gis',
    [string] $GisShare = '\\gis-sql.corp.local\GisDeploy$\Permits'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator

if (-not (Test-GeomatixEngine -InstallDir $InstallDir)) {
    throw "Geomatix Pro with an authorised Standard licence was not found in $InstallDir. Ask the GIS team to install and authorise it, or use -GisProvider Development on a training server."
}
foreach ($sub in 'Replica', 'Maps', 'Layouts', 'Basemap', 'Connections') { New-Item -ItemType Directory -Path (Join-Path $GisRoot $sub) -Force | Out-Null }
Copy-Item "$GisShare\Maps\Permits.gmapx" "$GisRoot\Maps\" -Force
Copy-Item "$GisShare\Layouts\*.glytx" "$GisRoot\Layouts\" -Force
Copy-Item "$GisShare\Basemap\OSMasterMap_Ashford.gtpk" "$GisRoot\Basemap\" -Force
Copy-Item "$GisShare\Connections\gis-sql_ASHFORD_GIS.gconn" "$GisRoot\Connections\" -Force

$replica = Join-Path $GisRoot 'Replica\Ashford.ggdb'
if (-not (Test-Path $replica)) {
    & (Join-Path $InstallDir 'bin\GeomatixReplica.exe') create /connection:"$GisRoot\Connections\gis-sql_ASHFORD_GIS.gconn" `
        /replica:"PERMITS_$($env:COMPUTERNAME.ToUpper())" /target:"$replica" /type:OneWayChild
    if ($LASTEXITCODE -ne 0) { throw 'Replica creation failed.' }
}
icacls $GisRoot /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
# CoreHost writes its licence cache and temp workspaces under the service account profile.
Write-Host "GIS engine ready on $env:COMPUTERNAME; replica $replica"
