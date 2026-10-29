<#
.SYNOPSIS
    Installs wkhtmltopdf 0.12.6 (used for CMR, bills of lading and delivery notes) and checks the native library.
.DESCRIPTION
    Needed on every server that renders documents: booking, scan and jobs01. The library path is
    configured with Documents:WkhtmltoxPath.
.EXAMPLE
    .\Install-Wkhtmltopdf.ps1 -Installer .\wkhtmltox-0.12.6-1.msvc2015-win64.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [string]$InstallPath = "$env:ProgramFiles\wkhtmltopdf"
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

$dll = Join-Path $InstallPath 'bin\wkhtmltox.dll'
if (-not (Test-Path $dll)) {
    Write-Step 'wkhtmltopdf'
    Start-Process -FilePath $Installer -ArgumentList '/S', "/D=$InstallPath" -Wait
}
if (-not (Test-Path $dll)) { throw "wkhtmltox.dll was not found in $InstallPath\bin." }

$version = (Get-Item $dll).VersionInfo.ProductVersion
Write-Host "  wkhtmltox.dll $version"
& (Join-Path $InstallPath 'bin\wkhtmltopdf.exe') --version

# The PDF templates use these fonts; they are part of Windows Server but not of Server Core.
foreach ($font in 'arial.ttf', 'arialbd.ttf', 'consola.ttf') {
    if (-not (Test-Path (Join-Path $env:windir "Fonts\$font"))) { Write-Warning "Font $font is missing." }
}

# Rendering test
$html = Join-Path $env:TEMP 'ct-pdf-test.html'
$pdf = Join-Path $env:TEMP 'ct-pdf-test.pdf'
'<html><body style="font-family:Arial"><h1>Control Tower</h1><p>PDF test</p></body></html>' | Set-Content $html
& (Join-Path $InstallPath 'bin\wkhtmltopdf.exe') --quiet $html $pdf
if (-not (Test-Path $pdf)) { throw 'wkhtmltopdf could not render the test page.' }
Remove-Item $html, $pdf
Write-Host ''
Write-Host "Set Documents:WkhtmltoxPath = $dll"
