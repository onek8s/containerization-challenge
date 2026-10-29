<#
.SYNOPSIS
    Installs the IIS sites for the inspections portal (inspections.<zone>) and the inspections service
    (inspections-svc.<zone>) under C:\inetpub\sites. Run as administrator on the web server(s).
    The portal needs IIS URL Rewrite 2.1 and Application Request Routing 3.0 (pass the installers or install them first).
.EXAMPLE
    .\Install-IisSites.ps1 -PublishPath ..\publish -ZoneName corp.local -CertificateThumbprint 3F2A... `
        -PortalPoolAccount CORP\svc-insp-web -ServicePoolAccount CORP\svc-insp-svc -PortalServerIPs 192.168.8.136
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$SiteRoot = 'C:\inetpub\sites',
    [string[]]$Sites = @('Inspections.Web', 'Inspections.Service'),
    [string]$CertificateThumbprint,
    [string]$PortalPoolAccount,
    [string]$ServicePoolAccount,
    [string[]]$PortalServerIPs = @(),
    [string]$UrlRewriteMsi,
    [string]$ArrMsi
)

$ErrorActionPreference = 'Stop'

Install-WindowsFeature Web-Server, Web-Asp-Net45, Web-Http-Redirect, Web-IP-Security, Web-Http-Logging, Web-Request-Monitor, Web-Mgmt-Console, NET-WCF-HTTP-Activation45 | Out-Null
foreach ($msi in @($UrlRewriteMsi, $ArrMsi)) {
    if ($msi) { Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn /norestart" -Wait }
}
Import-Module WebAdministration
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"

$definitions = @{
    'Inspections.Web'     = @{ Site = 'inspections';     HostName = "inspections.$ZoneName";     Account = $PortalPoolAccount }
    'Inspections.Service' = @{ Site = 'inspections-svc'; HostName = "inspections-svc.$ZoneName"; Account = $ServicePoolAccount }
}

New-Item -ItemType Directory -Force -Path "$env:ProgramData\Northwind\Inspections\Logs" | Out-Null

foreach ($name in $Sites) {
    $d = $definitions[$name]
    $physical = Join-Path $SiteRoot $d.Site
    $source = Join-Path $PublishPath $name
    if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }

    New-Item -ItemType Directory -Force -Path $physical | Out-Null
    robocopy $source $physical /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed." }

    $poolPath = "IIS:\AppPools\$($d.Site)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $d.Site | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 'Integrated'
    # The service decrypts its DPAPI secrets with the pool account's user profile.
    Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty $poolPath -Name processModel.setProfileEnvironment -Value $true
    Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value '00:00:00'
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value '00:00:00'

    if ($d.Account) {
        $credential = Get-Credential -UserName $d.Account -Message "Password for the $($d.Site) application pool account"
        Set-ItemProperty $poolPath -Name processModel -Value @{
            userName = $credential.UserName
            password = $credential.GetNetworkCredential().Password
            identityType = 3
        }
        icacls "$env:ProgramData\Northwind\Inspections\Logs" /grant "$($d.Account):(OI)(CI)M" | Out-Null
    }

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

    if ($name -eq 'Inspections.Web') {
        # Encrypt the connection string with the machine-level RSA key.
        & "$env:windir\Microsoft.NET\Framework64\v4.0.30319\aspnet_regiis.exe" -pef connectionStrings $physical | Out-Null
        if ($d.Account) {
            & "$env:windir\Microsoft.NET\Framework64\v4.0.30319\aspnet_regiis.exe" -pa NetFrameworkConfigurationKey $d.Account | Out-Null
        }
    }

    if ($name -eq 'Inspections.Service') {
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$($d.Site)/PhotoService.svc" `
            -Filter 'system.webServer/security/ipSecurity' -Name allowUnlisted -Value $false
        foreach ($ip in $PortalServerIPs) {
            Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$($d.Site)/PhotoService.svc" `
                -Filter 'system.webServer/security/ipSecurity' -Name '.' -Value @{ ipAddress = $ip; allowed = 'true' }
        }
    }
}

# ARR forwards /media from the portal to the service site and the rewrite rules set forwarding headers.
& $appcmd set config -section:system.webServer/proxy /enabled:"True" /preserveHostHeader:"False" /reverseRewriteHostInResponseHeaders:"False" /commit:apphost | Out-Null
& $appcmd set config 'inspections' -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_FORWARDED_HOST']" /commit:apphost 2>$null | Out-Null
& $appcmd set config 'inspections' -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_ORIGINAL_URL']" /commit:apphost 2>$null | Out-Null
& $appcmd unlock config -section:system.webServer/security/ipSecurity | Out-Null

Write-Host 'IIS sites installed.'
