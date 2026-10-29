<#
.SYNOPSIS
    Builds the solution in Release and publishes the web sites and tools to a folder. Run on the build machine
    (Visual Studio 2022 or Build Tools with the web build tools; msbuild.exe on the PATH).
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\drop\CustomerCare
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\publish'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

& msbuild.exe "$root\CustomerCare.sln" /t:Restore /p:Configuration=$Configuration /v:m
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

# Release builds precompile the .aspx pages and Razor views.
& msbuild.exe "$root\CustomerCare.sln" /t:Build /p:Configuration=$Configuration /v:m
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

foreach ($site in 'CustomerCare.Web', 'CustomerCare.Integration') {
    $target = Join-Path $OutputPath $site
    & msbuild.exe "$root\src\$site\$site.csproj" /t:WebPublish /p:Configuration=$Configuration `
        /p:WebPublishMethod=FileSystem /p:PublishUrl="$target" /p:DeleteExistingFiles=true /p:MvcBuildViews=false /v:m
    if ($LASTEXITCODE -ne 0) { throw "Publishing $site failed." }
}

foreach ($tool in 'CustomerCare.Import', 'CustomerCare.Archive') {
    $source = Join-Path $root "src\$tool\bin\$Configuration\net48"
    $target = Join-Path $OutputPath $tool
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item "$source\*" $target -Recurse -Force
}

Copy-Item (Join-Path $root 'db') (Join-Path $OutputPath 'db') -Recurse -Force
Copy-Item (Join-Path $root 'deploy') (Join-Path $OutputPath 'deploy') -Recurse -Force
Write-Host "Published to $OutputPath"
