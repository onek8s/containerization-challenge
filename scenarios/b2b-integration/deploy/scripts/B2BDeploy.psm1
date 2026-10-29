#Requires -Version 5.0
<#
    Shared functions of the VanArsdel B2B deployment scripts: logging, settings, secrets and tool wrappers.
    The log goes to $env:B2B_LOG_ROOT\deploy (set by Set-B2BEnvironment.ps1), or to the user's temp folder on a
    server that has not been prepared yet.
#>

Set-StrictMode -Version 2.0

$script:LogFile = $null
$script:DeployRoot = Split-Path -Parent $PSScriptRoot

function Initialize-B2BLog {
    param([Parameter(Mandatory)] [string] $Environment)

    $root = if ($env:B2B_LOG_ROOT) { Join-Path $env:B2B_LOG_ROOT 'deploy' } else { Join-Path $env:TEMP 'b2b-deploy' }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $script:LogFile = Join-Path $root ('deploy-{0}-{1:yyyyMMdd-HHmmss}.log' -f $Environment, (Get-Date))
    Write-B2BLog ('Deployment of VanArsdel.Integration to {0} by {1}\{2} on {3}' -f $Environment, $env:USERDOMAIN, $env:USERNAME, $env:COMPUTERNAME)
    return $script:LogFile
}

function Write-B2BLog {
    param(
        [Parameter(Mandatory, Position = 0)] [string] $Message,
        [ValidateSet('Info', 'Warning', 'Error', 'Step')] [string] $Level = 'Info'
    )

    $line = '{0:yyyy-MM-dd HH:mm:ss} {1,-7} {2}' -f (Get-Date), $Level.ToUpperInvariant(), $Message
    if ($script:LogFile) { Add-Content -Path $script:LogFile -Value $line -Encoding UTF8 }
    switch ($Level) {
        'Warning' { Write-Warning $Message }
        'Error'   { Write-Host $line -ForegroundColor Red }
        'Step'    { Write-Host ''; Write-Host "== $Message" -ForegroundColor Cyan }
        default   { Write-Host $line }
    }
}

function Get-B2BDeployRoot { $script:DeployRoot }

function Get-B2BSettings {
    <# Reads <ENV>_settings.xml as exported by b2bdeploy export-settings into a hashtable. #>
    param([Parameter(Mandatory)] [string] $Path)

    if (-not (Test-Path $Path)) { throw "Settings file $Path not found. Run b2bdeploy export-settings first." }
    $settings = @{}
    foreach ($p in ([xml](Get-Content -Path $Path -Raw)).settings.property) {
        $settings[$p.name] = $p.InnerText
    }
    return $settings
}

function Get-B2BSecrets {
    <#
        The secrets file is the JSON export from the password vault (one object, name/value). It is read once and
        should be deleted after the run; nothing from it is written to the log.
    #>
    param([string] $Path)

    $secrets = @{}
    if (-not $Path) { return $secrets }
    if (-not (Test-Path $Path)) { throw "Secrets file $Path not found." }
    $json = Get-Content -Path $Path -Raw | ConvertFrom-Json
    foreach ($p in $json.PSObject.Properties) { $secrets[$p.Name] = [string]$p.Value }
    return $secrets
}

function Get-B2BSecret {
    param([Parameter(Mandatory)] [hashtable] $Secrets, [Parameter(Mandatory)] [string] $Name, [switch] $Optional)

    if ($Secrets.ContainsKey($Name) -and $Secrets[$Name]) { return $Secrets[$Name] }
    if ($Optional) { return $null }
    throw "Secret '$Name' is missing from the secrets file."
}

function Test-B2BTrue {
    param([string] $Value)
    return [string]::Equals($Value, 'True', [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-B2BTool {
    <# Runs b2bdeploy.exe (VanArsdel.Integration.BindingTool) from the deployment folder or the build output. #>
    param([Parameter(Mandatory)] [string[]] $Arguments)

    $candidates = @(
        (Join-Path $script:DeployRoot 'b2bdeploy.exe'),
        (Join-Path $script:DeployRoot '..\src\VanArsdel.Integration.BindingTool\bin\Release\net472\b2bdeploy.exe')
    )
    $exe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $exe) { throw 'b2bdeploy.exe not found; build B2BIntegration.sln in Release first.' }

    Write-B2BLog ('b2bdeploy {0}' -f ($Arguments -join ' '))
    $output = & $exe @Arguments 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { Write-B2BLog ([string]$line) }
    if ($code -ne 0) { throw "b2bdeploy $($Arguments[0]) failed with exit code $code." }
}

function Invoke-B2BSqlScript {
    <# sqlcmd with -b (stop on error) and the given scripting variables; values are never logged. #>
    param(
        [Parameter(Mandatory)] [string] $Server,
        [Parameter(Mandatory)] [string] $Database,
        [Parameter(Mandatory)] [string] $File,
        [hashtable] $Variables = @{}
    )

    $arguments = @('-S', $Server, '-d', $Database, '-E', '-b', '-I', '-i', $File)
    foreach ($k in $Variables.Keys) { $arguments += @('-v', ('{0}={1}' -f $k, $Variables[$k])) }
    Write-B2BLog ('sqlcmd {0} on {1}/{2} ({3})' -f (Split-Path -Leaf $File), $Server, $Database, (($Variables.Keys | Sort-Object) -join ', '))
    $output = & sqlcmd.exe @arguments 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { if ("$line".Trim()) { Write-B2BLog ([string]$line) } }
    if ($code -ne 0) { throw "sqlcmd failed on $File with exit code $code." }
}

function Get-B2BBizTalkInstallPath {
    $key = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\BizTalk Server\3.0' -ErrorAction SilentlyContinue
    if (-not $key) { throw 'BizTalk Server is not installed on this computer.' }
    return $key.InstallPath
}

function Get-B2BBizTalkGroup {
    <# Management database of the group this server belongs to. #>
    $key = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\BizTalk Server\3.0\Administration' -ErrorAction SilentlyContinue
    if (-not $key) { throw 'This server is not joined to a BizTalk group.' }
    return [pscustomobject]@{ Server = $key.MgmtDBServer; Database = $key.MgmtDBName }
}

function Test-B2BAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-B2BServerList {
    param([string] $Value)
    return @(($Value -split ';') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Publish-B2BWebApplication {
    <#
        Copies a built web project (MSBuild.SDK.SystemWeb output: content files plus bin) to a web server, applies
        the Web.<Environment>.config transform when the project has one, and creates the app pool and the site.
        The physical path is inetpub\b2b\<Site> on the system drive of the target server.
    #>
    param(
        [Parameter(Mandatory)] [string] $Server,
        [Parameter(Mandatory)] [string] $ProjectFolder,
        [Parameter(Mandatory)] [string] $Site,
        [Parameter(Mandatory)] [string] $HostHeader,
        [Parameter(Mandatory)] [string] $Environment,
        [string] $AppPoolUser,
        [string] $AppPoolPassword,
        [string] $CertificateThumbprint,
        [bool] $UseHttps = $true,
        [ValidateSet('Anonymous', 'Windows')] [string] $Authentication = 'Anonymous',
        [scriptblock] $ConfigureWebConfig,
        [pscredential] $Credential,
        [scriptblock] $PostDeploy
    )

    $staging = Join-Path $env:TEMP ('b2b-web-{0}-{1}' -f $Site, [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    try {
        $exclude = @('obj', 'Properties', '*.cs', '*.csproj', '*.user', 'Web.*.config', 'packages.config', '*.pdb')
        robocopy $ProjectFolder $staging /E /NFL /NDL /NJH /NJS /XD obj Properties App_Data\Temp /XF @($exclude | Where-Object { $_ -like '*.*' }) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copying $ProjectFolder failed (robocopy $LASTEXITCODE)." }

        $transform = Join-Path $ProjectFolder ('Web.{0}.config' -f (Get-Culture).TextInfo.ToTitleCase($Environment.ToLowerInvariant()))
        if (Test-Path $transform) {
            Invoke-B2BConfigTransform -Config (Join-Path $staging 'Web.config') -Transform $transform
            Write-B2BLog "Applied $(Split-Path -Leaf $transform)"
        }
        if ($ConfigureWebConfig) {
            $path = Join-Path $staging 'Web.config'
            $xml = [xml](Get-Content -Path $path -Raw)
            & $ConfigureWebConfig $xml
            $xml.Save($path)
        }

        # DMZ servers are outside the domain: WinRM over HTTPS with a local administrator
        $session = if ($Credential) { New-PSSession -ComputerName $Server -Credential $Credential -UseSSL } else { New-PSSession -ComputerName $Server }
        try {
            $target = Invoke-Command -Session $session -ArgumentList $Site -ScriptBlock {
                param($site)
                $path = Join-Path $env:SystemDrive ('inetpub\b2b\' + $site)
                New-Item -ItemType Directory -Path $path -Force | Out-Null
                Get-ChildItem -Path $path -Exclude 'App_Data' | Remove-Item -Recurse -Force
                $path
            }
            Copy-Item -Path (Join-Path $staging '*') -Destination $target -ToSession $session -Recurse -Force

            Invoke-Command -Session $session -ArgumentList $Site, $target, $HostHeader, $AppPoolUser, $AppPoolPassword, $CertificateThumbprint, $UseHttps, $Authentication -ScriptBlock {
                param($site, $path, $hostHeader, $user, $password, $thumbprint, $useHttps, $authentication)
                Import-Module WebAdministration

                $pool = "IIS:\AppPools\$site"
                if (-not (Test-Path $pool)) { New-WebAppPool -Name $site | Out-Null }
                Set-ItemProperty $pool -Name managedRuntimeVersion -Value 'v4.0'
                Set-ItemProperty $pool -Name managedPipelineMode -Value 'Integrated'
                Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
                Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
                Set-ItemProperty $pool -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
                Set-ItemProperty $pool -Name recycling.periodicRestart.schedule -Value @{ value = '03:15:00' }
                if ($user -and $password) {
                    Set-ItemProperty $pool -Name processModel -Value @{ userName = $user; password = $password; identityType = 3 }
                }
                else {
                    Set-ItemProperty $pool -Name processModel.identityType -Value 4
                }

                $port = if ($useHttps) { 443 } else { 80 }
                $protocol = if ($useHttps) { 'https' } else { 'http' }
                if (-not (Test-Path "IIS:\Sites\$site")) {
                    New-Website -Name $site -PhysicalPath $path -ApplicationPool $site -HostHeader $hostHeader -Port $port -Ssl:$useHttps | Out-Null
                }
                else {
                    Set-ItemProperty "IIS:\Sites\$site" -Name physicalPath -Value $path
                }
                if ($useHttps) {
                    $binding = Get-WebBinding -Name $site -Protocol https
                    $binding.AddSslCertificate($thumbprint, 'My')
                    Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $site -Filter 'system.webServer/security/access' -Name sslFlags -Value 'Ssl'
                }

                $windows = $authentication -eq 'Windows'
                Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $site -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value (-not $windows)
                Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $site -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $windows
                if ($windows) {
                    # Kerberos with the app pool account (SPN HTTP/<host> registered on it), NTLM as fallback
                    Set-WebConfigurationProperty -PSPath 'IIS:\' -Location $site -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true
                }
                Restart-WebAppPool -Name $site
                "${protocol}://$hostHeader/ -> $path"
            } | ForEach-Object { Write-B2BLog $_ }

            if ($PostDeploy) { Invoke-Command -Session $session -ArgumentList $target -ScriptBlock $PostDeploy | ForEach-Object { Write-B2BLog $_ } }
        }
        finally { Remove-PSSession $session }
    }
    finally { Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue }
}

function Invoke-B2BConfigTransform {
    <# XDT transform with Microsoft.Web.XmlTransform (Visual Studio build tools or the B2B_XDT_ASSEMBLY path). #>
    param([Parameter(Mandatory)] [string] $Config, [Parameter(Mandatory)] [string] $Transform)

    $assembly = if ($env:B2B_XDT_ASSEMBLY) { $env:B2B_XDT_ASSEMBLY } else {
        Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio') -Filter 'Microsoft.Web.XmlTransform.dll' -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $assembly) { throw 'Microsoft.Web.XmlTransform.dll not found; set B2B_XDT_ASSEMBLY.' }
    Add-Type -Path $assembly

    $doc = New-Object Microsoft.Web.XmlTransform.XmlTransformableDocument
    $doc.PreserveWhitespace = $true
    $doc.Load($Config)
    $xdt = New-Object Microsoft.Web.XmlTransform.XmlTransformation($Transform)
    if (-not $xdt.Apply($doc)) { throw "Transform $Transform failed." }
    $doc.Save($Config)
}

Export-ModuleMember -Function *-B2B*
