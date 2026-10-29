<#
.SYNOPSIS
    Installs the PolicyDesk lab client on a test desk station in place of the PolicyDesk client.
.DESCRIPTION
    The lab client has the PolicyDesk 6.4 windows used by the flows and keeps its policies in
    %ProgramData%\PolicyDesk\lab\policies.json. Needs the .NET 8 Desktop Runtime.
    Publish first:
      dotnet publish tools\PolicyDesk.Lab -c Release -r win-x64 --self-contained false -o publish\lab-client
.EXAMPLE
    .\install-lab-client.ps1 -PublishPath ..\publish\lab-client
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $InstallPath = "${env:ProgramFiles(x86)}\Halcyon\PolicyDesk 6",
    [switch] $ResetData
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force

$data = Join-Path $env:ProgramData 'PolicyDesk\lab'
New-Item -ItemType Directory -Force -Path $data | Out-Null
icacls $data /grant 'BUILTIN\Users:(OI)(CI)M' | Out-Null
if ($ResetData) { Remove-Item (Join-Path $data 'policies.json') -ErrorAction SilentlyContinue }

Write-Host "Lab client installed to $InstallPath\HPDESK.EXE; policies in $data\policies.json"
