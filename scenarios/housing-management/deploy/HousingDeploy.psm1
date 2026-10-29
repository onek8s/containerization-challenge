# Shared helpers for the Fabrikam Housing install scripts. Run the scripts elevated on the target server.

$ErrorActionPreference = 'Stop'

function Get-HousingSourceRoot {
    Split-Path -Parent $PSScriptRoot
}

function Assert-HostingBundle {
    param([Parameter(Mandatory)] [string] $Version)
    Import-Module WebAdministration
    if (-not (Get-WebGlobalModule -Name 'AspNetCoreModuleV2' -ErrorAction SilentlyContinue)) {
        throw "AspNetCoreModuleV2 is not registered. Install the ASP.NET Core $Version Hosting Bundle."
    }
    $runtimes = & dotnet --list-runtimes 2>$null
    if (-not ($runtimes -match "Microsoft.AspNetCore.App $([regex]::Escape($Version))")) {
        throw "ASP.NET Core runtime $Version is not installed (dotnet --list-runtimes)."
    }
}

function Publish-HousingProject {
    param(
        [Parameter(Mandatory)] [string] $ProjectName,
        [Parameter(Mandatory)] [string] $Target,
        [string] $Configuration = 'Release'
    )
    $project = Join-Path (Get-HousingSourceRoot) "src\$ProjectName\$ProjectName.csproj"
    if (Test-Path (Join-Path $Target 'web.config')) {
        Set-Content -Path (Join-Path $Target 'app_offline.htm') -Value '<p>Fabrikam Homes is being updated. Please try again in a few minutes.</p>'
        Start-Sleep -Seconds 5
    }
    & dotnet publish $project -c $Configuration -o $Target | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publishing $ProjectName failed." }
}

function Complete-HousingPublish {
    param([Parameter(Mandatory)] [string] $Target)
    Remove-Item (Join-Path $Target 'app_offline.htm') -ErrorAction SilentlyContinue
}

function Set-HousingAppPool {
    param(
        [Parameter(Mandatory)] [string] $Name,
        # ApplicationPoolIdentity, or a group managed service account such as CORP\svc-housing-web$
        [string] $Identity = 'ApplicationPoolIdentity',
        [switch] $AlwaysRunning
    )
    Import-Module WebAdministration
    if (-not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null }
    $path = "IIS:\AppPools\$Name"
    Set-ItemProperty $path -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $path -Name processModel.loadUserProfile -Value $true
    if ($Identity -eq 'ApplicationPoolIdentity') {
        Set-ItemProperty $path -Name processModel.identityType -Value ApplicationPoolIdentity
    } else {
        # gMSA: specific user with an empty password, IIS retrieves the managed password from AD.
        Set-ItemProperty $path -Name processModel -Value @{ identityType = 3; userName = $Identity; password = '' }
    }
    if ($AlwaysRunning) {
        Set-ItemProperty $path -Name startMode -Value AlwaysRunning
        Set-ItemProperty $path -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
        Set-ItemProperty $path -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
    }
}

function Get-HousingPoolAccount {
    param([Parameter(Mandatory)] [string] $Pool, [string] $Identity = 'ApplicationPoolIdentity')
    if ($Identity -eq 'ApplicationPoolIdentity') { "IIS AppPool\$Pool" } else { $Identity }
}

function Set-HousingSite {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $HostName,
        [Parameter(Mandatory)] [string] $PhysicalPath,
        [Parameter(Mandatory)] [string] $AppPool,
        [string] $CertificateThumbprint,
        [switch] $Preload
    )
    Import-Module WebAdministration
    if (-not (Get-Website -Name $Name)) {
        New-Website -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $AppPool -HostHeader $HostName -Port 80 | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $AppPool
    }
    if ($CertificateThumbprint) {
        if (-not (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName)) {
            New-WebBinding -Name $Name -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
        }
        (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName).AddSslCertificate($CertificateThumbprint, 'My')
    }
    if ($Preload) {
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationDefaults.preloadEnabled -Value $true
    }
}

function Grant-HousingFolder {
    param(
        [Parameter(Mandatory)] [string[]] $Path,
        [Parameter(Mandatory)] [string] $Account,
        [ValidateSet('R', 'RX', 'M', 'F')] [string] $Rights = 'M'
    )
    foreach ($p in $Path) {
        New-Item -ItemType Directory -Path $p -Force | Out-Null
        & icacls $p /grant "${Account}:(OI)(CI)$Rights" /T /Q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls failed for $p" }
    }
}

function Write-HousingSettings {
    <#
        Writes appsettings.Production.json next to the application. The file holds passwords and keys, so only
        administrators and the application account may read it.
    #>
    param(
        [Parameter(Mandatory)] [string] $Target,
        [Parameter(Mandatory)] [System.Collections.IDictionary] $Settings,
        [Parameter(Mandatory)] [string] $Account
    )
    $file = Join-Path $Target 'appsettings.Production.json'
    $Settings | ConvertTo-Json -Depth 8 | Set-Content -Path $file -Encoding UTF8
    & icacls $file /inheritance:r /grant:r 'BUILTIN\Administrators:F' "${Account}:R" | Out-Null
}

function ConvertTo-PlainText {
    param([Parameter(Mandatory)] [securestring] $Value)
    [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value))
}

function Grant-CertificatePrivateKey {
    <# Gives an account read access to the private key of a certificate in LocalMachine\My. #>
    param(
        [Parameter(Mandatory)] [string] $Thumbprint,
        [Parameter(Mandatory)] [string] $Account
    )
    $cert = Get-Item "Cert:\LocalMachine\My\$Thumbprint"
    $rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    if ($rsa -is [System.Security.Cryptography.RSACng]) {
        $keyFile = Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$($rsa.Key.UniqueName)"
    } else {
        $keyFile = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$($rsa.CspKeyContainerInfo.UniqueKeyContainerName)"
    }
    & icacls $keyFile /grant "${Account}:R" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not grant $Account read access to $keyFile" }
    Write-Host "Granted $Account read access to the private key of $($cert.Subject)"
}

function Grant-LogOnAsService {
    <#
        Adds an account to "Log on as a service" (SeServiceLogonRight). The current policy is exported and the
        account is merged into the existing list, so other accounts keep the right.
    #>
    param([Parameter(Mandatory)] [string] $Account)
    $sid = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $work = Join-Path $env:TEMP ("secedit-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $export = Join-Path $work 'export.inf'
        & secedit /export /cfg $export /areas USER_RIGHTS | Out-Null
        $lines = Get-Content $export -Encoding Unicode
        $current = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
        if ($current -and $current -match [regex]::Escape("*$sid")) {
            Write-Host "$Account already has Log on as a service"
            return
        }
        $value = if ($current) { ($current -split '=', 2)[1].Trim() + ",*$sid" } else { "*$sid" }
        $import = Join-Path $work 'import.inf'
        @(
            '[Unicode]', 'Unicode=yes',
            '[Version]', 'signature="$CHICAGO$"', 'Revision=1',
            '[Privilege Rights]', "SeServiceLogonRight = $value"
        ) | Set-Content -Path $import -Encoding Unicode
        & secedit /configure /db (Join-Path $work 'secedit.sdb') /cfg $import /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'secedit /configure failed.' }
        Write-Host "Granted Log on as a service to $Account"
    } finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Install-HousingWebApp {
    <#
        Publishes one of the web applications to C:\inetpub\sites\<Name>, creates its app pool and site,
        creates its data folders with modify rights for the pool account and writes appsettings.Production.json.
    #>
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $ProjectName,
        [Parameter(Mandatory)] [string] $HostName,
        [Parameter(Mandatory)] [string] $RuntimeVersion,
        [Parameter(Mandatory)] [System.Collections.IDictionary] $Settings,
        [string] $SiteRoot = 'C:\inetpub\sites',
        [string] $Identity = 'ApplicationPoolIdentity',
        [string[]] $DataFolders = @(),
        [string] $CertificateThumbprint,
        [switch] $AlwaysRunning
    )
    Assert-HostingBundle -Version $RuntimeVersion
    $target = Join-Path $SiteRoot $Name
    Publish-HousingProject -ProjectName $ProjectName -Target $target
    Set-HousingAppPool -Name $Name -Identity $Identity -AlwaysRunning:$AlwaysRunning
    $account = Get-HousingPoolAccount -Pool $Name -Identity $Identity
    Write-HousingSettings -Target $target -Settings $Settings -Account $account
    Grant-HousingFolder -Path (@((Join-Path $target 'logs')) + $DataFolders) -Account $account
    Set-HousingSite -Name $Name -HostName $HostName -PhysicalPath $target -AppPool $Name -CertificateThumbprint $CertificateThumbprint -Preload:$AlwaysRunning
    Complete-HousingPublish -Target $target
    Restart-WebAppPool -Name $Name
    $scheme = if ($CertificateThumbprint) { 'https' } else { 'http' }
    Write-Host "$Name published: ${scheme}://$HostName/health"
    return $account
}

function Test-WkHtmlToPdf {
    param([Parameter(Mandatory)] [string] $Target)
    if (-not (Test-Path (Join-Path $Target 'native\libwkhtmltox.dll'))) {
        Write-Warning "native\libwkhtmltox.dll is missing in $Target. Letters fall back to the basic PDF renderer. Copy libwkhtmltox.dll (wkhtmltopdf 0.12.6, 64-bit) there."
    }
}

Export-ModuleMember -Function *
