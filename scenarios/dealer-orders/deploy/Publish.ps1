<#
.SYNOPSIS
    Builds the solution with MSBuild (Visual Studio 2017 Build Tools) and publishes the web applications and services
    to a drop folder: <Output>\<Project>\...
.EXAMPLE
    .\Publish.ps1 -Output C:\Build\DealerOrders -Configuration Release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Output,
    [string]$Configuration = 'Release'
)

. "$PSScriptRoot\common.ps1"

$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'DealerOrders.sln'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild not found. Install the Visual Studio Build Tools with the web development workload.' }

Write-Step "Restore and build $Configuration"
& $msbuild $solution /t:Restore /v:minimal
& $msbuild $solution /t:Build /p:Configuration=$Configuration /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

foreach ($app in $WebApps.Values) {
    Write-Step "Publish $($app.Project)"
    $project = Join-Path $root "src\$($app.Project)\$($app.Project).csproj"
    $target = Join-Path $Output $app.Project
    # WebPublish copies Views, Content, Scripts and *.svc with the bin folder (the default Build target does not).
    & $msbuild $project /p:Configuration=$Configuration /p:DeployOnBuild=true /p:DeployDefaultTarget=WebPublish `
        /p:WebPublishMethod=FileSystem /p:PublishUrl=$target /p:DeleteExistingFiles=true /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Publish of $($app.Project) failed." }
}

foreach ($svc in $Services.Values) {
    Write-Step "Copy $($svc.Project)"
    $bin = Join-Path $root "src\$($svc.Project)\bin\$Configuration\net472"
    $target = Join-Path $Output $svc.Project
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $bin $target -Recurse
}

$lab = Join-Path $Output 'Tailspin.DealerOrders.LabTools'
Copy-Item (Join-Path $root "src\Tailspin.DealerOrders.LabTools\bin\$Configuration\net472") $lab -Recurse -Force
Copy-Item (Join-Path $root 'db') (Join-Path $Output 'db') -Recurse -Force
Copy-Item (Join-Path $root 'templates') (Join-Path $Output 'templates') -Recurse -Force
Write-Host "Published to $Output"
