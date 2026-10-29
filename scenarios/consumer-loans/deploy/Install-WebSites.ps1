<#
.SYNOPSIS
  Creates the IIS sites apply, lending-api and lending-office (and lending-lab in the lab) and copies
  the published applications from .\out. Needs the IIS role and the ASP.NET Core 6 Hosting Bundle.
#>
param(
    [string] $Config = (Join-Path $PSScriptRoot 'environment.psd1'),
    [string] $Secrets,
    [string] $Source = (Join-Path $PSScriptRoot '..\out'),
    [string[]] $Sites = @('apply', 'lending-api', 'lending-office')
)
Import-Module (Join-Path $PSScriptRoot 'Common.psm1') -Force
$cfg = Import-DeployConfig $Config $Secrets
Import-Module WebAdministration

if (-not (Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2')) {
    throw 'ASP.NET Core Hosting Bundle (AspNetCoreModuleV2) is not installed.'
}
if ((Test-Lab $cfg) -and $Sites -notcontains 'lending-lab') { $Sites += 'lending-lab' }

$thumbprint = $cfg.CertificateThumbprint
if (-not $thumbprint) {
    $names = $Sites | ForEach-Object { Get-HostName $cfg $_ }
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$($names[0])" -and $_.NotAfter -gt (Get-Date).AddDays(30) } | Select-Object -First 1
    if (-not $cert) {
        Write-Step 'Creating self-signed lab certificate'
        $cert = New-SelfSignedCertificate -DnsName $names -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddYears(2)
        $root = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
        $root.Open('ReadWrite'); $root.Add($cert); $root.Close()
    }
    $thumbprint = $cert.Thumbprint
}

$api = "https://$(Get-HostName $cfg 'lending-api')/"
$lab = "https://$(Get-HostName $cfg 'lending-lab')"
$logs = $cfg.LogRoot
$core = Get-CoreSettings $cfg

$settings = @{
    'apply' = @{
        ConnectionStrings = @{ PortalIdentity = Get-SqlConnectionString $cfg $cfg.Sql.PortalDatabase 'lending_portal' 'Lending Customer Portal' }
        Database          = @{ Provider = 'SqlServer'; Upgrade = $true }
        LendingApi        = @{ BaseUrl = $api; ApiKey = (Get-Secret $cfg 'ApiKey.Portal' 'lab-portal-key') }
        Portal            = @{ RequireTwoFactor = $(if ($cfg.ContainsKey('PortalRequireTwoFactor')) { $cfg.PortalRequireTwoFactor } else { $true }) }
        DataProtection    = @{ KeysFolder = Join-Path $cfg.InstallRoot 'Portal\Keys' }
        NLog              = Get-LogSettings (Join-Path $logs 'Portal')
    }
    'lending-api' = @{
        ConnectionStrings = @{ Lending = Get-SqlConnectionString $cfg $cfg.Sql.LendingDatabase 'lending_api' 'Lending API' }
        Database          = @{ Provider = 'SqlServer'; Upgrade = $true }
        Core              = $core
        Bureau            = @{ Endpoint = $cfg.Urls.Bureau; Password = (Get-Secret $cfg 'Bureau.Password' 'test') }
        ESign             = @{ BaseUrl = $cfg.Urls.ESign; ApiKey = (Get-Secret $cfg 'ESign.ApiKey' 'lab-esign-key'); CallbackSecret = (Get-Secret $cfg 'ESign.CallbackSecret' 'lab-callback-secret');
                               CallbackUrl = "$($api)api/esign/callback"; ReturnUrl = "https://$(Get-HostName $cfg 'apply')/applications" }
        PaymentsHub       = @{ BaseUrl = $cfg.Urls.PaymentsHub; ClientSecret = (Get-Secret $cfg 'PaymentsHub.ClientSecret' 'lab') }
        Seed              = @{ ApiClients = @{
                                 'customer-portal' = @{ Key = (Get-Secret $cfg 'ApiKey.Portal' 'lab-portal-key'); Scopes = 'portal' }
                                 'back-office'     = @{ Key = (Get-Secret $cfg 'ApiKey.Office' 'lab-office-key'); Scopes = 'office' } } }
        Swagger           = @{ Enabled = (Test-Lab $cfg) }
        NLog              = Get-LogSettings (Join-Path $logs 'Api')
    }
    'lending-office' = @{
        ConnectionStrings = @{ Lending = Get-SqlConnectionString $cfg $cfg.Sql.LendingDatabase 'lending_office' 'Lending Back Office' }
        Core              = $core
        Auth              = $(if ($cfg.ContainsKey('OfficeAuthMode')) { @{ Mode = $cfg.OfficeAuthMode } } else { @{ Mode = 'WsFederation'; MetadataAddress = $cfg.Urls.Adfs; Realm = "https://$(Get-HostName $cfg 'lending-office')/" } })
        PaymentsHub       = @{ BaseUrl = $cfg.Urls.PaymentsHub; ClientSecret = (Get-Secret $cfg 'PaymentsHub.ClientSecret' 'lab') }
        NLog              = Get-LogSettings (Join-Path $logs 'BackOffice')
    }
    'lending-lab' = @{
        Core  = @{ SimulatorDatabase = $cfg.Core.SimulatorDatabase }
        Stubs = @{ PublicUrl = $lab; ESignApiKey = (Get-Secret $cfg 'ESign.ApiKey' 'lab-esign-key'); ESignCallbackSecret = (Get-Secret $cfg 'ESign.CallbackSecret' 'lab-callback-secret') }
    }
}

foreach ($site in $Sites) {
    Write-Step $site
    $path = New-Folder (Join-Path $cfg.SitesRoot $site)
    $identity = $cfg.AppPools[$site]

    if (-not (Test-Path "IIS:\AppPools\$site")) { New-WebAppPool -Name $site | Out-Null }
    $pool = "IIS:\AppPools\$site"
    Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty $pool -Name processModel.loadUserProfile -Value $true
    if ($identity -and $identity -ne 'ApplicationPoolIdentity') {
        if ($identity.EndsWith('$')) {
            Set-ItemProperty $pool -Name processModel -Value @{ identityType = 3; userName = $identity; password = '' }
        } else {
            Set-ItemProperty $pool -Name processModel -Value @{ identityType = 3; userName = $identity; password = (Get-Secret $cfg "AppPool.$site") }
        }
        $account = $identity
    } else {
        $account = "IIS AppPool\$site"
    }

    Stop-WebAppPool -Name $site -ErrorAction SilentlyContinue
    robocopy (Join-Path $Source $site) $path /MIR /XF appsettings.Production.json /XD logs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy to $path failed ($LASTEXITCODE)." }
    Write-ProductionSettings $path $settings[$site]

    $log = Join-Path $logs ($site -replace '^lending-', '')
    Grant-FolderAccess $log $account
    if ($site -eq 'apply') { Grant-FolderAccess (Join-Path $cfg.InstallRoot 'Portal\Keys') $account }
    if ($cfg.Core.Provider -eq 'Simulator' -and $site -in 'lending-api', 'lending-office', 'lending-lab') {
        Grant-FolderAccess (Split-Path $cfg.Core.SimulatorDatabase) $account
    }

    $hostName = Get-HostName $cfg $site
    if (-not (Get-Website -Name $site)) {
        New-Website -Name $site -PhysicalPath $path -ApplicationPool $site -HostHeader $hostName -Port 443 -Ssl -SslFlags 1 | Out-Null
        Get-WebBinding -Name $site -Protocol https | ForEach-Object { $_.AddSslCertificate($thumbprint, 'My') }
        New-WebBinding -Name $site -Protocol http -Port 80 -HostHeader $hostName
    }
    Set-ItemProperty "IIS:\Sites\$site" -Name applicationDefaults.preloadEnabled -Value $true
    Start-WebAppPool -Name $site
    Write-Host "    https://$hostName/ (pool identity $account)"
}
