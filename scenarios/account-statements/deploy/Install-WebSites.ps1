<#
.SYNOPSIS
    Installs the IIS sites for the statements portal (statements.<zone>, Windows authentication) and the document API
    (statements-api.<zone>, anonymous with API keys) under C:\inetpub\sites. Run as administrator on the web server.
.EXAMPLE
    .\Install-WebSites.ps1 -PublishPath C:\Build\AccountStatements -ZoneName corp.local -PortalPoolAccount CORP\svc-stm-web -ApiPoolAccount CORP\svc-stm-api
.EXAMPLE
    .\Install-WebSites.ps1 -PublishPath C:\Build\AccountStatements -ZoneName corp.local -PortalPoolAccount CORP\svc-stm-web -ApiPoolAccount CORP\svc-stm-api -CertificateThumbprint 3F2A... -Sites Api
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$PortalPoolAccount,
    [Parameter(Mandatory)] [string]$ApiPoolAccount,
    [ValidateSet('Portal', 'Api')] [string[]]$Sites = @('Portal', 'Api'),
    [string]$SiteRoot = 'C:\inetpub\sites',
    [string]$SqlServer = 'sql01.corp.local',
    [string]$FileServer = 'files01.corp.local',
    [string]$Domain = 'CORP',
    [string]$LogFolder = 'D:\AccountStatements\Logs',
    [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'

Install-WindowsFeature Web-Server, Web-Asp-Net45, Web-Windows-Auth, Web-Http-Logging, Web-Request-Monitor, Web-Mgmt-Console | Out-Null
Import-Module WebAdministration
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
New-Item -ItemType Directory -Force -Path $LogFolder | Out-Null

$definitions = @{
    'Portal' = @{ Site = 'statements';     HostName = "statements.$ZoneName";     Account = $PortalPoolAccount; Windows = $true }
    'Api'    = @{ Site = 'statements-api'; HostName = "statements-api.$ZoneName"; Account = $ApiPoolAccount;    Windows = $false }
}

foreach ($name in $Sites) {
    $d = $definitions[$name]
    $physical = Join-Path $SiteRoot $d.Site
    $source = Join-Path $PublishPath $name
    if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }

    New-Item -ItemType Directory -Force -Path $physical | Out-Null
    $configFile = Join-Path $physical 'Web.config'
    $keep = Test-Path $configFile
    if ($keep) { Copy-Item $configFile "$configFile.bak" -Force }
    robocopy $source $physical /MIR /NFL /NDL /NJH /NJS /XF *.pdb /XF Web.config.bak | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed." }
    if ($keep) { Copy-Item "$configFile.bak" $configFile -Force }

    # Settings for this environment
    [xml]$config = Get-Content $configFile
    $cs = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AccountStatements' }
    $cs.connectionString = $cs.connectionString -replace 'Data Source=[^;]+', "Data Source=$SqlServer"
    foreach ($add in @($config.configuration.appSettings.add)) {
        if ($add.key -like 'Roles.*') { $add.value = $add.value -replace '^[^\\]+\\', "$Domain\" }
        if ($add.key -eq 'ArchiveRoot') { $add.value = "\\$FileServer\StatementArchive$" }
    }
    $listener = $config.configuration.'system.diagnostics'.trace.listeners.add | Where-Object { $_.name -eq 'file' }
    if ($listener) { $listener.initializeData = [string](Join-Path $LogFolder ([IO.Path]::GetFileName($listener.initializeData))) }
    $config.Save($configFile)

    $poolPath = "IIS:\AppPools\$($d.Site)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $d.Site | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty $poolPath -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value '00:00:00'
    Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true
    # Report rendering holds documents in memory; recycle at night instead of every 29 hours.
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value '00:00:00'
    Clear-ItemProperty $poolPath -Name recycling.periodicRestart.schedule
    New-ItemProperty $poolPath -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' } | Out-Null
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.privateMemory -Value 4194304

    $credential = Get-Credential -UserName $d.Account -Message "Password for the $($d.Site) application pool account"
    Set-ItemProperty $poolPath -Name processModel -Value @{
        userName = $credential.UserName
        password = $credential.GetNetworkCredential().Password
        identityType = 3
    }
    icacls $LogFolder /grant "$($d.Account):(OI)(CI)M" | Out-Null

    if (-not (Get-Website -Name $d.Site)) {
        New-Website -Name $d.Site -PhysicalPath $physical -ApplicationPool $d.Site -HostHeader $d.HostName -Port 80 | Out-Null
    }
    else {
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name physicalPath -Value $physical
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name applicationPool -Value $d.Site
    }

    if ($d.Windows) {
        & $appcmd unlock config -section:system.webServer/security/authentication/windowsAuthentication | Out-Null
        & $appcmd unlock config -section:system.webServer/security/authentication/anonymousAuthentication | Out-Null
        # Kerberos with the pool account: the portal host name needs SPN HTTP/statements.<zone> on the pool account.
        Set-WebConfigurationProperty -PSPath IIS:\ -Location $d.Site -Filter system.webServer/security/authentication/windowsAuthentication -Name useAppPoolCredentials -Value $true
        Write-Host "Register the SPN once: setspn -S HTTP/$($d.HostName) $($d.Account)"
    }
    else {
        & $appcmd unlock config -section:system.webServer/security/authentication/anonymousAuthentication | Out-Null
        & $appcmd unlock config -section:system.webServer/security/authentication/windowsAuthentication | Out-Null
    }

    if ($CertificateThumbprint -and -not (Get-WebBinding -Name $d.Site -Protocol https)) {
        New-WebBinding -Name $d.Site -Protocol https -Port 443 -HostHeader $d.HostName -SslFlags 1
        (Get-WebBinding -Name $d.Site -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
    }
    Write-Host "Site $($d.Site) installed at $physical ($($d.HostName))."
}
