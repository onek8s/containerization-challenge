<#
.SYNOPSIS
    Installs the Field Sales IIS sites on a web server: the API (salesapi), the sales office site (salesadmin) and the
    ClickOnce deployment site (salesapp). Copies the files from Publish.ps1 output to C:\inetpub\sites\<site>,
    creates the application pools, HTTPS bindings and writes the environment settings into Web.config.
    Run as administrator. Use -Sites to install only some of them on a server.
.EXAMPLE
    .\Install-IisSites.ps1 -PublishPath C:\Deploy\FieldSales -ZoneName corp.local -SqlServer sql01.corp.local `
        -SqlLogin fieldsales_app
.NOTES
    When the API runs on more than one server, install the first one, then copy the machine key file it writes
    (-MachineKeyPath) to the other servers so that tokens issued by one server are accepted by the others.
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [Parameter(Mandatory)] [string]$SqlServer,
    [string]$SqlLogin = 'fieldsales_app',
    [SecureString]$SqlPassword,
    [string[]]$Sites = @('Api', 'Office', 'Deployment'),
    [string]$SiteRoot = 'C:\inetpub\sites',
    [string]$LogFolder = 'C:\Logs\FieldSales',
    [string]$CertificateThumbprint,
    [string]$MachineKeyPath = 'C:\ProgramData\Wingtip\FieldSales\machinekey.xml',
    [string]$ErpApiKey,
    [SecureString]$WarehousePassword,
    [string]$MinimumClientVersion,
    [string]$SmtpPickupFolder
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$definitions = [ordered]@{
    'Api'        = @{ Source = 'FieldSales.Api';        Site = 'FieldSalesApi';    HostName = "salesapi.$ZoneName" }
    'Office'     = @{ Source = 'FieldSales.Backoffice'; Site = 'FieldSalesOffice'; HostName = "salesadmin.$ZoneName" }
    'Deployment' = @{ Source = 'FieldSales.Client';     Site = 'FieldSalesApp';    HostName = "salesapp.$ZoneName" }
}

function ConvertTo-Plain([SecureString]$value) {
    if (-not $value) { return '' }
    [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($value))
}

function New-RandomHex([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    -join ($buffer | ForEach-Object { $_.ToString('X2') })
}

function Get-MachineKey {
    if (Test-Path $MachineKeyPath) { return Import-Clixml $MachineKeyPath }
    $key = [pscustomobject]@{ ValidationKey = New-RandomHex 64; DecryptionKey = New-RandomHex 32 }
    New-Item -ItemType Directory -Force -Path (Split-Path $MachineKeyPath) | Out-Null
    $key | Export-Clixml $MachineKeyPath
    icacls $MachineKeyPath /inheritance:r /grant 'BUILTIN\Administrators:F' 'NT AUTHORITY\SYSTEM:F' | Out-Null
    Write-Host "Generated machine key, saved to $MachineKeyPath (copy it to the other web servers)."
    $key
}

function Set-AppSetting([xml]$config, [string]$key, [string]$value) {
    $node = $config.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if (-not $node) {
        $node = $config.CreateElement('add')
        $node.SetAttribute('key', $key)
        $config.configuration.appSettings.AppendChild($node) | Out-Null
    }
    $node.SetAttribute('value', $value)
}

function Get-Certificate {
    if ($CertificateThumbprint) { return Get-Item "Cert:\LocalMachine\My\$CertificateThumbprint" }

    $names = $definitions.Values | ForEach-Object { $_.HostName }
    $existing = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.FriendlyName -eq 'Field Sales' -and $_.NotAfter -gt (Get-Date).AddDays(30) } | Select-Object -First 1
    if ($existing) { return $existing }

    Write-Warning 'No certificate given; creating a self-signed certificate for the lab. Laptops must trust it (Group Policy).'
    $cert = New-SelfSignedCertificate -DnsName $names -CertStoreLocation Cert:\LocalMachine\My -FriendlyName 'Field Sales' `
        -NotAfter (Get-Date).AddYears(3) -KeyExportPolicy Exportable
    $rootStore = New-Object Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
    $rootStore.Open('ReadWrite'); $rootStore.Add($cert); $rootStore.Close()
    Export-Certificate -Cert $cert -FilePath (Join-Path $PublishPath 'fieldsales-web.cer') | Out-Null
    $cert
}

$sqlPasswordPlain = ConvertTo-Plain $SqlPassword
if ($Sites -contains 'Api' -or $Sites -contains 'Office') {
    if (-not $sqlPasswordPlain) { $sqlPasswordPlain = ConvertTo-Plain (Read-Host "Password for SQL login $SqlLogin" -AsSecureString) }
    $machineKey = Get-MachineKey
}
if ($Sites -contains 'Api' -and -not $ErpApiKey) {
    $ErpApiKey = New-RandomHex 24
    Write-Host "Generated ERP API key: $ErpApiKey (configure it in the ERP integration jobs)."
}

$certificate = Get-Certificate
New-Item -ItemType Directory -Force -Path $LogFolder | Out-Null

foreach ($name in $Sites) {
    $d = $definitions[$name]
    if (-not $d) { throw "Unknown site $name. Use Api, Office or Deployment." }

    $source = Join-Path $PublishPath $d.Source
    if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }
    $physical = Join-Path $SiteRoot $d.Site
    New-Item -ItemType Directory -Force -Path $physical | Out-Null
    robocopy $source $physical /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $($d.Source) failed." }

    $poolPath = "IIS:\AppPools\$($d.Site)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $d.Site | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value $(if ($name -eq 'Deployment') { '' } else { 'v4.0' })
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty $poolPath -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

    if (-not (Get-Website -Name $d.Site)) {
        New-Website -Name $d.Site -PhysicalPath $physical -ApplicationPool $d.Site -HostHeader $d.HostName -Port 443 -Ssl -SslFlags 1 | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name physicalPath -Value $physical
    }
    $sslPath = "IIS:\SslBindings\!443!$($d.HostName)"
    if (Test-Path $sslPath) { Remove-Item $sslPath }
    $binding = Get-WebBinding -Name $d.Site -Protocol https -HostHeader $d.HostName
    $binding.AddSslCertificate($certificate.Thumbprint, 'My')

    $filter = 'system.webServer/security/authentication'
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter "$filter/anonymousAuthentication" -Name enabled -Value $true

    if ($name -ne 'Deployment') {
        $configPath = Join-Path $physical 'Web.config'
        [xml]$config = Get-Content $configPath
        $cs = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'FieldSales' }
        $cs.SetAttribute('connectionString', "Data Source=$SqlServer;Initial Catalog=FieldSales;User ID=$SqlLogin;Password=$sqlPasswordPlain;MultipleActiveResultSets=True;Application Name=$($d.Source)")

        $config.configuration.'system.web'.compilation.SetAttribute('debug', 'false')
        $mk = $config.configuration.'system.web'.machineKey
        $mk.SetAttribute('validationKey', $machineKey.ValidationKey)
        $mk.SetAttribute('decryptionKey', $machineKey.DecryptionKey)

        $smtp = $config.configuration.'system.net'.mailSettings.smtp
        if ($SmtpPickupFolder) {
            New-Item -ItemType Directory -Force -Path $SmtpPickupFolder | Out-Null
            icacls $SmtpPickupFolder /grant "IIS AppPool\$($d.Site):(OI)(CI)M" | Out-Null
            $smtp.SetAttribute('deliveryMethod', 'SpecifiedPickupDirectory')
            $pickup = $config.CreateElement('specifiedPickupDirectory')
            $pickup.SetAttribute('pickupDirectoryLocation', $SmtpPickupFolder)
            $smtp.AppendChild($pickup) | Out-Null
        } else {
            $smtp.network.SetAttribute('host', "smtp.$ZoneName")
        }

        foreach ($appender in $config.configuration.log4net.appender) {
            if ($appender.file) { $appender.file.SetAttribute('value', (Join-Path $LogFolder (Split-Path $appender.file.value -Leaf))) }
            if ($appender.smtpHost) { $appender.smtpHost.SetAttribute('value', "smtp.$ZoneName") }
        }

        if ($name -eq 'Api') {
            Set-AppSetting $config 'FieldSales:AllowInsecureHttp' 'false'
            Set-AppSetting $config 'FieldSales:ErpApiKey' $ErpApiKey
            Set-AppSetting $config 'FieldSales:WarehouseApiUrl' "http://wms.$ZoneName/wms/api/v2/"
            if ($WarehousePassword) { Set-AppSetting $config 'FieldSales:WarehouseApiPassword' (ConvertTo-Plain $WarehousePassword) }
            if ($MinimumClientVersion) { Set-AppSetting $config 'FieldSales:MinimumClientVersion' $MinimumClientVersion }
        } else {
            Set-AppSetting $config 'FieldSales:ClientInstallUrl' "https://salesapp.$ZoneName/publish.htm"
        }

        $config.Save($configPath)
        icacls $configPath /inheritance:r /grant 'BUILTIN\Administrators:F' 'NT AUTHORITY\SYSTEM:F' "IIS AppPool\$($d.Site):R" | Out-Null
        icacls $LogFolder /grant "IIS AppPool\$($d.Site):(OI)(CI)M" | Out-Null
    }

    Write-Host "$($d.Source) installed: https://$($d.HostName)/ -> $physical"
}
