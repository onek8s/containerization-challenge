<#
.SYNOPSIS
    Writes HKLM\SOFTWARE\Litware\DocControl\<Component> with InstallPath and DataPath. The applications
    read their templates from InstallPath and keep work folders under DataPath.
.EXAMPLE
    .\Set-InstallLocation.ps1 -Component Packager -InstallPath 'C:\Program Files\Litware\DocControl\Packager' -DataPath D:\DocControl\Packager
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Web', 'Packager', 'Indexer')] [string]$Component,
    [Parameter(Mandatory)] [string]$InstallPath,
    [Parameter(Mandatory)] [string]$DataPath
)

$ErrorActionPreference = 'Stop'

$key = "HKLM:\SOFTWARE\Litware\DocControl\$Component"
New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name InstallPath -Value $InstallPath
Set-ItemProperty -Path $key -Name DataPath -Value $DataPath
Write-Host "$key -> $InstallPath, $DataPath"
