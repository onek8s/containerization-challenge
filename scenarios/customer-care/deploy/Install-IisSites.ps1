<#
.SYNOPSIS
    Installs the Customer Care portal (care.<zone>) and the integration service (care-api.<zone>) as IIS sites
    under C:\inetpub\sites. Run as administrator on the web server(s).
.PARAMETER Sites
    Which sites to install on this server; both by default.
.PARAMETER NotifyKey
    Shared key the integration service uses to tell the portal about changes. Generated when omitted;
    use the same value on every server.
.EXAMPLE
    .\Install-IisSites.ps1 -PublishPath ..\ -ZoneName corp.local -PortalPoolAccount CORP\svc-care-web -ApiPoolAccount CORP\svc-care-api
.EXAMPLE
    .\Install-IisSites.ps1 -PublishPath C:\drop\CustomerCare -ZoneName corp.local -CertificateThumbprint 3F2A... -Sites CustomerCare.Web -NotifyKey 'k3y...'
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$SiteRoot = 'C:\inetpub\sites',
    [ValidateSet('CustomerCare.Web', 'CustomerCare.Integration')]
    [string[]]$Sites = @('CustomerCare.Web', 'CustomerCare.Integration'),
    [string]$PortalPoolAccount,
    [string]$ApiPoolAccount,
    [string]$CertificateThumbprint,
    [string]$NotifyKey,
    [string]$Domain = 'CORP'
)

$ErrorActionPreference = 'Stop'

Install-WindowsFeature Web-Server, Web-Asp-Net45, Web-Windows-Auth, Web-WebSockets, Web-AppInit, Web-Http-Logging, `
    Web-Request-Monitor, Web-Mgmt-Console, NET-WCF-HTTP-Activation45 | Out-Null
Import-Module WebAdministration

if (-not $NotifyKey) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $NotifyKey = [Convert]::ToBase64String($bytes)
    Write-Host "Generated notify key (use it on the other servers): $NotifyKey"
}

$definitions = @{
    'CustomerCare.Web'         = @{ Site = 'care';     HostName = "care.$ZoneName";     Account = $PortalPoolAccount }
    'CustomerCare.Integration' = @{ Site = 'care-api'; HostName = "care-api.$ZoneName"; Account = $ApiPoolAccount }
}

$logs = "$env:ProgramData\Tailspin\CustomerCare\Logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

foreach ($name in $Sites) {
    $d = $definitions[$name]
    $physical = Join-Path $SiteRoot $d.Site
    $source = Join-Path $PublishPath $name
    if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }

    New-Item -ItemType Directory -Force -Path $physical | Out-Null
    robocopy $source $physical /MIR /NFL /NDL /NJH /NJS /XF *.pdb /XD App_Data | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed." }

    # Zone, domain and the shared key in web.config.
    $configPath = Join-Path $physical 'Web.config'
    [xml]$config = Get-Content $configPath
    foreach ($add in $config.configuration.appSettings.add) {
        if ($add.key -eq 'CustomerCare:NotifyKey') { $add.value = $NotifyKey }
        $add.value = $add.value.Replace('corp.local', $ZoneName).Replace('CORP\', "$Domain\")
        if ($add.key -eq 'CustomerCare:Domain') { $add.value = $Domain }
        # Without a certificate the portal has only the http binding.
        if (-not $CertificateThumbprint -and $add.key -in 'CustomerCare:PortalUrl', 'CustomerCare:PortalNotifyUrl') {
            $add.value = $add.value.Replace('https://', 'http://')
        }
    }
    foreach ($cs in $config.configuration.connectionStrings.add) { $cs.connectionString = $cs.connectionString.Replace('corp.local', $ZoneName) }
    $smtp = $config.configuration.'system.net'.mailSettings.smtp.network
    if ($smtp) { $smtp.host = $smtp.host.Replace('corp.local', $ZoneName) }
    $config.Save($configPath)

    $poolPath = "IIS:\AppPools\$($d.Site)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $d.Site | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value '00:00:00'
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value '00:00:00'
    # Recycle at night only; the portal keeps sessions, the live board connections and the job server in the process.
    Clear-ItemProperty $poolPath -Name recycling.periodicRestart.schedule
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' }
    Set-ItemProperty $poolPath -Name processModel.maxProcesses -Value 1

    if ($d.Account) {
        $credential = Get-Credential -UserName $d.Account -Message "Password for the $($d.Site) application pool account"
        Set-ItemProperty $poolPath -Name processModel -Value @{
            userName     = $credential.UserName
            password     = $credential.GetNetworkCredential().Password
            identityType = 3
        }
        $identity = $d.Account
    }
    else {
        $identity = "IIS APPPOOL\$($d.Site)"
    }
    icacls $logs /grant "$($identity):(OI)(CI)M" | Out-Null

    if (-not (Get-Website -Name $d.Site)) {
        New-Website -Name $d.Site -PhysicalPath $physical -ApplicationPool $d.Site -HostHeader $d.HostName -Port 80 | Out-Null
    }
    else {
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name physicalPath -Value $physical
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name applicationPool -Value $d.Site
    }

    if ($CertificateThumbprint -and -not (Get-WebBinding -Name $d.Site -Protocol https)) {
        New-WebBinding -Name $d.Site -Protocol https -Port 443 -HostHeader $d.HostName -SslFlags 1
        (Get-WebBinding -Name $d.Site -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
    }

    # Windows authentication only.
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $false
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $true

    if ($name -eq 'CustomerCare.Web') {
        # Background jobs run inside the portal: start it with the server and keep it running.
        Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name applicationDefaults.preloadEnabled -Value $true

        # Web.config turns on the WebSocket module for SignalR; the section is locked at server level by default.
        & "$env:windir\System32\inetsrv\appcmd.exe" unlock config $d.Site -section:system.webServer/webSocket -commit:apphost | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Unlocking the webSocket section failed.' }

        # The integration service calls /internal/notify.ashx with the shared key.
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$($d.Site)/internal" -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $true

        foreach ($folder in 'App_Data', 'App_Data\Reports') {
            New-Item -ItemType Directory -Force -Path (Join-Path $physical $folder) | Out-Null
        }
        icacls (Join-Path $physical 'App_Data') /grant "$($identity):(OI)(CI)M" | Out-Null
    }
}

Write-Host 'IIS sites installed.'
foreach ($name in $Sites) { Write-Host "  http://$($definitions[$name].HostName)/" }
