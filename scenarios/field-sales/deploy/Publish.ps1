<#
.SYNOPSIS
    Builds the Release configuration and publishes the API, the sales office site and the ClickOnce client.
    Run on the build machine from a Developer PowerShell for Visual Studio 2017 or later (msbuild on the path).
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Deploy\FieldSales -ZoneName corp.local -ClientVersion 3.2.0.41 `
        -SigningThumbprint 3F1A...C2
#>
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$ClientVersion,
    [string]$SigningThumbprint,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'FieldSales.sln'

if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) { throw 'msbuild not found. Use the Developer PowerShell for Visual Studio.' }
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

msbuild $solution /t:Restore /p:Configuration=$Configuration /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

foreach ($web in 'FieldSales.Api', 'FieldSales.Backoffice') {
    $project = Join-Path $root "src\$web\$web.csproj"
    $target = Join-Path $OutputPath $web
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    msbuild $project /t:WebPublish /p:Configuration=$Configuration /p:WebPublishMethod=FileSystem `
        /p:PublishUrl=$target /p:DeleteExistingFiles=true /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Publishing $web failed." }
    Write-Host "$web -> $target"
}

# ClickOnce. The version must grow with every publish or the laptops do not see the update.
$client = Join-Path $root 'src\FieldSales.Client\FieldSales.Client.csproj'
$clientOut = Join-Path $OutputPath 'FieldSales.Client'
if (Test-Path $clientOut) { Remove-Item $clientOut -Recurse -Force }

$properties = @(
    "/p:Configuration=$Configuration",
    "/p:PublishProtocol=ClickOnce",
    "/p:GenerateManifests=true",
    "/p:ApplicationVersion=$ClientVersion",
    "/p:MinimumRequiredVersion=$ClientVersion",
    "/p:InstallUrl=https://salesapp.$ZoneName/",
    "/p:UpdateUrl=https://salesapp.$ZoneName/",
    "/p:ApiBaseUrl=https://salesapi.$ZoneName/",
    "/p:SupportUrl=https://salesadmin.$ZoneName/",
    "/p:PublishDir=$clientOut\"
)
if ($SigningThumbprint) {
    $properties += '/p:SignManifests=true', "/p:ManifestCertificateThumbprint=$SigningThumbprint"
} else {
    Write-Warning 'Manifests are not signed. Laptops will show an unknown publisher warning on install.'
}

msbuild $client /t:Publish @properties /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'ClickOnce publish failed.' }

Copy-Item (Join-Path $PSScriptRoot 'clickonce\web.config') $clientOut
# Command line ClickOnce publishing does not write the install page.
$page = Join-Path $clientOut 'publish.htm'
if (-not (Test-Path $page)) {
    @"
<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>Wingtip Foods Field Sales</title></head>
<body style="font-family:Segoe UI,sans-serif">
<h1>Field Sales $ClientVersion</h1>
<p><a href="FieldSales.application">Install Field Sales</a></p>
<p>If .NET Framework 4.7.2 is missing, run <a href="setup.exe">setup.exe</a> first.</p>
</body></html>
"@ | Set-Content $page -Encoding UTF8
}
Write-Host "FieldSales.Client $ClientVersion -> $clientOut"
Write-Host "Set FieldSales:MinimumClientVersion in the API Web.config to $ClientVersion to force the update on the next sync."
