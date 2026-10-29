<#
.SYNOPSIS
    Builds Lucerne.ProjectSites.Dashboards.app (an OPC package) for the farm's app catalog.
.DESCRIPTION
    The .app file is a zip with [Content_Types].xml, AppManifest.xml and a feature package with the app parts.
    Upload it to the app catalog (https://projects.corp.local/sites/apps) and add it to the hub and project sites.
#>
param(
    [Parameter(Mandatory = $true)] [string] $OutputPath,
    [Parameter(Mandatory = $true)] [string] $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName WindowsBase

$fourPart = if (($Version -split '\.').Count -eq 3) { "$Version.0" } else { $Version }
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$target = Join-Path $OutputPath 'Lucerne.ProjectSites.Dashboards.app'
Remove-Item $target -ErrorAction SilentlyContinue

[xml] $manifest = Get-Content (Join-Path $PSScriptRoot 'AppManifest.xml')
$manifest.App.Version = $fourPart

$package = [System.IO.Packaging.Package]::Open($target, [IO.FileMode]::Create)
try {
    $manifestPart = $package.CreatePart([Uri]'/AppManifest.xml', 'text/xml', [System.IO.Packaging.CompressionOption]::Normal)
    $stream = $manifestPart.GetStream(); $manifest.Save($stream); $stream.Dispose()
    $package.CreateRelationship($manifestPart.Uri, [System.IO.Packaging.TargetMode]::Internal,
        'http://schemas.microsoft.com/sharepoint/2012/app/relationships/manifest') | Out-Null

    $elementsPart = $package.CreatePart([Uri]'/Elements/AppParts.xml', 'text/xml', [System.IO.Packaging.CompressionOption]::Normal)
    $bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Elements\AppParts.xml'))
    $stream = $elementsPart.GetStream(); $stream.Write($bytes, 0, $bytes.Length); $stream.Dispose()
    $manifestPart.CreateRelationship($elementsPart.Uri, [System.IO.Packaging.TargetMode]::Internal,
        'http://schemas.microsoft.com/sharepoint/2012/app/relationships/partfile') | Out-Null
} finally {
    $package.Close()
}
Write-Host "Created $target ($fourPart)"
