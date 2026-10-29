<#
.SYNOPSIS
    Builds the Release configuration and publishes the web application and the three services to an output folder.
    Requires the .NET 8 SDK and the .NET Framework 4.8 developer pack (Visual Studio 2022 or Build Tools).
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Build\SupplierInvoices
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\publish')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
$OutputPath = Resolve-Path $OutputPath

dotnet test "$root\tests\Invoices.Core.Tests" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

dotnet publish "$root\src\Invoices.Admin\Invoices.Admin.csproj" -c Release -o "$OutputPath\Invoices.Admin"
if ($LASTEXITCODE -ne 0) { throw 'Publishing Invoices.Admin failed.' }

foreach ($service in 'Invoices.Gateway', 'Invoices.Processor', 'Invoices.ErpLink') {
    dotnet build "$root\src\$service\$service.csproj" -c Release -o "$OutputPath\$service"
    if ($LASTEXITCODE -ne 0) { throw "Building $service failed." }
    Remove-Item "$OutputPath\$service\*.pdb" -ErrorAction SilentlyContinue
}

Copy-Item "$root\db" "$OutputPath\db" -Recurse -Force
Copy-Item "$root\deploy" "$OutputPath\deploy" -Recurse -Force
Write-Host "Published to $OutputPath"
