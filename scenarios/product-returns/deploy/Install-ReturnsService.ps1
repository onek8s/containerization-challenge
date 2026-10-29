<#
.SYNOPSIS
    Installs one of the Product Returns Windows services from the publish folder:
    Portal (returns-web), Api (returns-api) or Labels (returns-jobs). Run as administrator on the target server.
    The ASP.NET Core runtime for the role must be installed first (3.1 for Portal, 5.0 for Api and Labels).
.EXAMPLE
    .\Install-ReturnsService.ps1 -Role Api    -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-api
    .\Install-ReturnsService.ps1 -Role Portal -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-web -ZoneName corp.local
    .\Install-ReturnsService.ps1 -Role Labels -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-labels -CarrierAccount 400012345
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Portal', 'Api', 'Labels')] [string]$Role,
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$ZoneName = 'corp.local',
    [string]$SqlServer = "sql01.$ZoneName",
    [string]$FileServer = "files01.$ZoneName",
    [string]$InstallRoot = 'C:\Program Files\Wide World Importers\Product Returns',
    [string]$DataRoot = 'D:\ReturnsData',
    [string]$CarrierAccount,
    [string]$WebProxy = "http://proxy.${ZoneName}:8080"
)

$ErrorActionPreference = 'Stop'

$definition = @{
    Portal = @{ Name = 'WWIReturnsPortal'; Display = 'WWI Product Returns Portal'; Exe = 'ProductReturns.Portal'; Port = 5011; Host = "returns-web.$ZoneName"; Source = 'Product Returns Portal'; Description = 'Customer returns web site and staff queue.' }
    Api    = @{ Name = 'WWIReturnsApi';    Display = 'WWI Product Returns API';    Exe = 'ProductReturns.Api';    Port = 5021; Host = "returns-api.$ZoneName"; Source = 'Product Returns API';    Description = 'Returns API for the portal and the warehouse scanners.' }
    Labels = @{ Name = 'WWIReturnsLabels'; Display = 'WWI Product Returns Labels'; Exe = 'ProductReturns.Labels'; Port = $null; Host = $null;                  Source = 'Product Returns Labels'; Description = 'Books carrier return labels for approved returns.' }
}[$Role]

$serviceName = $definition.Name
$installPath = Join-Path $InstallRoot $definition.Exe
$settingsFolder = 'C:\ProgramData\WideWorldImporters\ProductReturns'
$settingsFile = Join-Path $settingsFolder "$($Role.ToLowerInvariant()).settings.json"
$credential = Get-Credential -UserName $ServiceAccount -Message "Password for $serviceName"
$password = $credential.GetNetworkCredential().Password

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

# Program files and local data.
New-Item -ItemType Directory -Force -Path $installPath, $settingsFolder | Out-Null
robocopy (Join-Path $PublishPath $definition.Exe) $installPath /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying $($definition.Exe) failed." }

$dataFolders = @{
    Portal = @("$DataRoot\Portal\Keys")
    Api    = @("$DataRoot\Api\Cache")
    Labels = @("$DataRoot\Labels")
}[$Role]
New-Item -ItemType Directory -Force -Path $dataFolders | Out-Null
icacls $DataRoot /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

# Machine settings: host names for this zone. Kept outside the program folder so updates do not overwrite them.
$settings = @{}
$previous = $null
if (Test-Path $settingsFile) {
    $previous = Get-Content $settingsFile -Raw | ConvertFrom-Json
}
$connection = "Server=$SqlServer;Database=ProductReturns;Integrated Security=true;Application Name=$($definition.Exe)"
switch ($Role) {
    'Portal' {
        $proxyAddress = (Resolve-DnsName "returns.$ZoneName" -Type A | Select-Object -First 1).IPAddress
        $settings['Api'] = @{ BaseUrl = "https://returns-api.${ZoneName}:5021/"; UseDefaultCredentials = $true }
        $settings['Proxy'] = @{ KnownProxies = @($proxyAddress) }
        $settings['AllowedHosts'] = "returns.$ZoneName;returns-web.$ZoneName"
        $settings['StaffDirectory'] = @{ Domain = $ZoneName; NetbiosDomain = $ServiceAccount.Split('\')[0] }
        $settings['DataProtection'] = @{ KeysFolder = "$DataRoot\Portal\Keys" }
    }
    'Api' {
        $settings['ConnectionStrings'] = @{ Returns = $connection }
        $settings['Security'] = @{ PortalAccounts = @($ServiceAccount.Split('\')[0] + '\svc-returns-web') }
        $settings['CatalogCache'] = @{ Folder = "$DataRoot\Api\Cache" }
    }
    'Labels' {
        $settings['ConnectionStrings'] = @{ Returns = $connection }
        $settings['Labels'] = @{ SharePath = "\\$FileServer\Returns$\Labels"; StateFolder = "$DataRoot\Labels" }
        $settings['Mail'] = @{ SmtpHost = "smtp.$ZoneName" }
        $carrier = @{ Proxy = $WebProxy }
        if ($CarrierAccount) { $carrier['AccountNumber'] = $CarrierAccount }
        $apiKey = Read-Host -AsSecureString 'Carrier API key (leave empty to keep the current one)'
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey))
        if ($plain) {
            # Encrypted with the machine key of this server; protect again after moving the service.
            $carrier['ApiKeyProtected'] = (& (Join-Path $installPath "$($definition.Exe).exe") protect-secret $plain).Trim()
        }
        elseif ($previous -and $previous.Carrier -and $previous.Carrier.ApiKeyProtected) {
            $carrier['ApiKeyProtected'] = $previous.Carrier.ApiKeyProtected
        }
        $settings['Carrier'] = $carrier
    }
}
if ($definition.Host) {
    $settings['Kestrel'] = @{ Endpoints = @{ Https = @{ Url = "https://*:$($definition.Port)"; Certificate = @{ Subject = $definition.Host; Store = 'My'; Location = 'LocalMachine'; AllowInvalid = $false } } } }
}
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8
icacls $settingsFile /inheritance:r /grant 'BUILTIN\Administrators:F' 'NT AUTHORITY\SYSTEM:F' "${ServiceAccount}:R" | Out-Null

# Kestrel reads the server certificate from LocalMachine\My; the service account needs its private key.
if ($definition.Host) {
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$($definition.Host)" -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if (-not $cert) { throw "Certificate CN=$($definition.Host) not found in LocalMachine\My." }
    $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    $keyFile = Get-ChildItem "$env:ProgramData\Microsoft\Crypto" -Recurse -Filter $key.Key.UniqueName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($keyFile) { icacls $keyFile.FullName /grant "${ServiceAccount}:R" | Out-Null }
}

# Event log source used by the service.
if (-not [System.Diagnostics.EventLog]::SourceExists($definition.Source)) {
    New-EventLog -LogName Application -Source $definition.Source
}

# Log on as a service right.
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'secpol.cfg'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$lines = Get-Content $cfg -Encoding Unicode
$line = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
if (-not $line) {
    $lines = $lines -replace '^\[Privilege Rights\]$', "[Privilege Rights]`r`nSeServiceLogonRight = *$sid"
}
elseif ($line -notlike "*$sid*") {
    $lines = $lines -replace [regex]::Escape($line), "$line,*$sid"
}
Set-Content $cfg $lines -Encoding Unicode
secedit /configure /db "$env:TEMP\secpol.sdb" /cfg $cfg /areas USER_RIGHTS | Out-Null
Remove-Item $cfg -ErrorAction SilentlyContinue

$exe = Join-Path $installPath "$($definition.Exe).exe"
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $password DisplayName= $definition.Display | Out-Null
}
else {
    sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $password | Out-Null
}
sc.exe description $serviceName $definition.Description | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/30000/restart/60000/restart/300000 | Out-Null

if ($definition.Port) {
    $rule = "$serviceName TCP $($definition.Port)"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $definition.Port -Action Allow | Out-Null
    }
}

if ($Role -eq 'Api') {
    # The portal authenticates with Kerberos to HTTP/returns-api.<zone>, which must belong to the API service account.
    setspn -S "HTTP/returns-api.$ZoneName" $ServiceAccount | Out-Null
}

Start-Service $serviceName
Get-Service $serviceName
