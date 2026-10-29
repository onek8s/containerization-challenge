<#
.SYNOPSIS
    Builds a SharePoint solution package (.wsp) from a farm project's build output with makecab.exe.
.DESCRIPTION
    The farm projects are SDK-style class libraries, not Visual Studio SharePoint projects, so the package
    is laid out here:
      - Package\manifest.xml: the public key token is replaced with the token of the built assembly and the
        third-party/facade assemblies of the build output are added at the ADDITIONAL_ASSEMBLIES marker
        (Microsoft.SharePoint* and other farm assemblies are excluded, they are already in the GAC).
      - Features\<Name>\*  ->  <Project>_<Name>\*   (feature folders in the 16 hive)
      - Layouts\*          ->  LAYOUTS\*
    makecab needs a DDF (directive file) listing every file with its path inside the cabinet.
.EXAMPLE
    .\New-SolutionPackage.ps1 -Project Lucerne.ProjectSites.Farm -OutputPath C:\Temp\farm
#>
param(
    [Parameter(Mandatory = $true)] [string] $Project,
    [Parameter(Mandatory = $true)] [string] $OutputPath,
    [string] $Configuration = 'Release',
    [string] $TargetFramework = 'net462'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $root "src\$Project"
$binDir = Join-Path $projectDir "bin\$Configuration\$TargetFramework"
$mainAssembly = Join-Path $binDir "$Project.dll"
if (-not (Test-Path $mainAssembly)) { throw "$mainAssembly not found. Build the solution first." }

$work = Join-Path $env:TEMP ("wsp-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

# Public key token of the strong-named assembly, as lower-case hex.
$token = ([Reflection.AssemblyName]::GetAssemblyName($mainAssembly).GetPublicKeyToken() | ForEach-Object { $_.ToString('x2') }) -join ''

# Assemblies that are part of SharePoint or the .NET Framework on the farm servers.
$excluded = '^(Microsoft\.SharePoint|Microsoft\.Office|Microsoft\.IdentityModel\.Extensions|Microsoft\.Web\.CommandUI|System\.Web\.)'

$files = New-Object System.Collections.Generic.List[object]
$assemblies = Get-ChildItem $binDir -Filter *.dll | Where-Object { $_.Name -notmatch $excluded }
$additional = foreach ($a in $assemblies) {
    $files.Add(@{ Source = $a.FullName; Target = $a.Name })
    if ($a.BaseName -ne $Project -and -not (Select-String -Path (Join-Path $projectDir 'Package\manifest.xml') -Pattern ('Location="' + [regex]::Escape($a.Name) + '"') -Quiet)) {
        # Unsigned facades cannot go to the GAC; netstandard facades and NuGet packages used here are signed.
        '    <Assembly Location="{0}" DeploymentTarget="GlobalAssemblyCache" />' -f $a.Name
    }
}

$manifest = Get-Content (Join-Path $projectDir 'Package\manifest.xml') -Raw
$manifest = $manifest.Replace('$SharePoint.Project.AssemblyPublicKeyToken$', $token)
$manifest = $manifest.Replace('<!-- ADDITIONAL_ASSEMBLIES -->', ($additional -join [Environment]::NewLine))
$manifestPath = Join-Path $work 'manifest.xml'
[IO.File]::WriteAllText($manifestPath, $manifest, (New-Object Text.UTF8Encoding($false)))
$files.Add(@{ Source = $manifestPath; Target = 'manifest.xml' })

$featuresDir = Join-Path $projectDir 'Features'
if (Test-Path $featuresDir) {
    foreach ($feature in Get-ChildItem $featuresDir -Directory) {
        foreach ($f in Get-ChildItem $feature.FullName -File -Recurse) {
            $relative = $f.FullName.Substring($feature.FullName.Length + 1)
            $files.Add(@{ Source = $f.FullName; Target = "$($Project)_$($feature.Name)\$relative" })
        }
    }
}

$layoutsDir = Join-Path $projectDir 'Layouts'
if (Test-Path $layoutsDir) {
    foreach ($f in Get-ChildItem $layoutsDir -File -Recurse) {
        $files.Add(@{ Source = $f.FullName; Target = 'LAYOUTS\' + $f.FullName.Substring($layoutsDir.Length + 1) })
    }
}

$wspName = "$Project.wsp"
$ddf = New-Object System.Text.StringBuilder
[void]$ddf.AppendLine('.OPTION EXPLICIT')
[void]$ddf.AppendLine(".Set CabinetNameTemplate=$wspName")
[void]$ddf.AppendLine(".Set DiskDirectoryTemplate=$OutputPath")
[void]$ddf.AppendLine('.Set CompressionType=MSZIP')
[void]$ddf.AppendLine('.Set Cabinet=on')
[void]$ddf.AppendLine('.Set Compress=on')
[void]$ddf.AppendLine('.Set UniqueFiles=off')
[void]$ddf.AppendLine('.Set MaxDiskSize=0')
[void]$ddf.AppendLine(".Set RptFileName=$work\setup.rpt")
[void]$ddf.AppendLine(".Set InfFileName=$work\setup.inf")
foreach ($group in $files | Group-Object { Split-Path $_.Target -Parent }) {
    [void]$ddf.AppendLine(".Set DestinationDir=$($group.Name)")
    foreach ($f in $group.Group) {
        [void]$ddf.AppendLine(('"{0}" "{1}"' -f $f.Source, (Split-Path $f.Target -Leaf)))
    }
}
$ddfPath = Join-Path $work 'package.ddf'
[IO.File]::WriteAllText($ddfPath, $ddf.ToString(), [Text.Encoding]::ASCII)

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
& makecab.exe /F $ddfPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makecab failed for $wspName." }
Remove-Item $work -Recurse -Force
Write-Host "Created $(Join-Path $OutputPath $wspName) ($($files.Count) files, public key token $token)"
