<#
.SYNOPSIS
    Installs or updates the LucerneProjectSync Windows service on sync01 with InstallUtil.
.EXAMPLE
    .\Install-SyncService.ps1 -Version 5.3.1 -PfxPath \\fs01\Install$\ProjectSites\Certificates\ProjectSitesS2S.pfx
#>
param(
    [string] $Version,
    [Parameter(Mandatory = $true)] [string] $PfxPath,
    [securestring] $PfxPassword = (Read-Host -AsSecureString 'PFX password'),
    [pscredential] $ServiceCredential = (Get-Credential -Message 'Service account' -UserName 'CORP\svc-ps-sync')
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Assert-Elevated
$settings = Get-ProjectSitesSettings
$drop = Get-ProjectSitesDrop -Settings $settings -Version $Version
$serviceName = 'LucerneProjectSync'
$exe = Join-Path $settings.SyncPath 'LucerneProjectSync.exe'
$installUtil = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'InstallUtil.exe'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Write-Step 'Stopping the service'
    Stop-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromMinutes(2))
}

Write-Step 'Signing certificate'
$signing = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $PfxPassword -Exportable:$false
$keyFile = $signing.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
& icacls.exe (Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$keyFile") /grant "$($ServiceCredential.UserName):R" | Out-Null

Write-Step 'Files'
New-Item -ItemType Directory -Path $settings.SyncPath, $settings.SyncLogs -Force | Out-Null
& robocopy.exe (Join-Path $drop 'sync') $settings.SyncPath /MIR /XF LucerneProjectSync.exe.config sync-state.json /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)." }
$configPath = "$exe.config"
if (-not (Test-Path $configPath)) { Copy-Item (Join-Path $drop 'sync\LucerneProjectSync.exe.config') $configPath }
[xml] $config = Get-Content $configPath
($config.configuration.appSettings.add | Where-Object key -eq 'SigningCertificateThumbprint').value = $signing.Thumbprint
$config.Save($configPath)
& icacls.exe $settings.SyncLogs /grant "$($ServiceCredential.UserName):(OI)(CI)M" | Out-Null

# The event log source must be created by an administrator; the service account cannot create it.
if (-not [Diagnostics.EventLog]::SourceExists('LucerneProjectSync')) {
    New-EventLog -LogName Application -Source 'LucerneProjectSync'
}

if (-not $service) {
    Write-Step 'Registering the service with InstallUtil'
    $password = $ServiceCredential.GetNetworkCredential().Password
    & $installUtil /LogToConsole=false /LogFile= "/username=$($ServiceCredential.UserName)" "/password=$password" /unattended $exe | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'InstallUtil failed.' }
    & sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
    & sc.exe config $serviceName start= delayed-auto | Out-Null
}

Write-Step 'Checking connectivity with a single run'
& $exe /once | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Warning 'The single run reported failures; see the log before starting the service.' }

Start-Service -Name $serviceName
Write-Step "$serviceName is $((Get-Service $serviceName).Status)"
