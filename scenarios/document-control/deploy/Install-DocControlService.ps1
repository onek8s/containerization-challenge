<#
.SYNOPSIS
    Installs the Document Control Packager (doccontrol-pkg) or Indexer (doccontrol-idx) Windows service.
    Run as administrator on the target server. Requires the .NET 8 runtime.
.EXAMPLE
    .\Install-DocControlService.ps1 -Component Packager -PublishPath C:\Build\DocControl\Packager -ServiceAccount CORP\svc-dc-packager
.EXAMPLE
    .\Install-DocControlService.ps1 -Component Indexer -PublishPath C:\Build\DocControl\Indexer -ServiceAccount CORP\svc-dc-indexer -DocTrackLibrary 'C:\Program Files (x86)\DocTrack\Viewer\dtzip.dll'
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Packager', 'Indexer')] [string]$Component,
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$ZoneName = 'corp.local',
    [string]$SqlServer = "sql01.$ZoneName",
    [string]$FileServer = "files01.$ZoneName",
    [string]$InstallRoot = 'C:\Program Files\Litware\DocControl',
    [string]$DataRoot = 'D:\DocControl',
    [string]$DocTrackLibrary
)

$ErrorActionPreference = 'Stop'

$definition = @{
    Packager = @{ Name = 'LitwareDocPackager'; Display = 'Litware Document Control Packager'; Exe = 'Litware.DocControl.Packager.exe'; Source = 'Litware DocControl Packager'; Description = 'Builds and mails transmittal packages.' }
    Indexer  = @{ Name = 'LitwareDocIndexer';  Display = 'Litware Document Control Indexer';  Exe = 'Litware.DocControl.Indexer.exe';  Source = 'Litware DocControl Indexer';  Description = 'Registers revisions dropped on the DocDrop share.' }
}[$Component]

$installPath = Join-Path $InstallRoot $Component
$dataPath = Join-Path $DataRoot $Component
$credential = Get-Credential -UserName $ServiceAccount -Message "Password for $($definition.Name)"

if (Get-Service $definition.Name -ErrorAction SilentlyContinue) {
    Stop-Service $definition.Name -Force
}

New-Item -ItemType Directory -Force -Path $installPath, $dataPath, (Join-Path $dataPath 'work') | Out-Null
robocopy $PublishPath $installPath /MIR /NFL /NDL /NJH /NJS /XF appsettings.Production.json dtzip.dll *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying $Component failed ($LASTEXITCODE)." }
if ($DocTrackLibrary) { Copy-Item $DocTrackLibrary $installPath -Force }
icacls $dataPath /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

$storage = @{ VaultRoot = "\\$FileServer\DocVault$"; TransmittalRoot = "\\$FileServer\Transmittals$"; DropRoot = "\\$FileServer\DocDrop$" }
$settings = [ordered]@{
    DocControl = [ordered]@{
        Database = @{ Provider = 'SqlServer'; ConnectionString = "Server=$SqlServer;Database=DocControl;Integrated Security=True;TrustServerCertificate=True;Application Name=DocControl.$Component" }
        Storage  = $storage
    }
}
if ($Component -eq 'Packager') {
    $settings.DocControl['Packager'] = @{ PortalUrl = "https://doccontrol.$ZoneName"; Mail = @{ Host = "smtp.$ZoneName" } }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $installPath 'appsettings.Production.json') -Encoding UTF8

& (Join-Path $PSScriptRoot 'Set-InstallLocation.ps1') -Component $Component -InstallPath $installPath -DataPath $dataPath
& (Join-Path $PSScriptRoot 'Set-SecurityPolicyRight.ps1') -Account $ServiceAccount -Right SeServiceLogonRight

if (-not [System.Diagnostics.EventLog]::SourceExists($definition.Source)) {
    New-EventLog -LogName Application -Source $definition.Source
}

$binary = '"{0}"' -f (Join-Path $installPath $definition.Exe)
if (-not (Get-Service $definition.Name -ErrorAction SilentlyContinue)) {
    New-Service -Name $definition.Name -DisplayName $definition.Display -Description $definition.Description `
        -BinaryPathName $binary -StartupType Automatic -Credential $credential | Out-Null
} else {
    sc.exe config $definition.Name binPath= $binary obj= $ServiceAccount password= $credential.GetNetworkCredential().Password | Out-Null
}
sc.exe failure $definition.Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null

# ASPNETCORE/DOTNET environment for the service process
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$($definition.Name)"
Set-ItemProperty -Path $serviceKey -Name Environment -Type MultiString -Value @('DOTNET_ENVIRONMENT=Production')

Start-Service $definition.Name
Get-Service $definition.Name
