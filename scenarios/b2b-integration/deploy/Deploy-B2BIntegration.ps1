#Requires -Version 5.0
<#
.SYNOPSIS
    Deploys VanArsdel.Integration to one environment.

.DESCRIPTION
    Runs the deployment steps in order. Every step can be run on its own (-Steps) and is safe to repeat.

      Settings      export the environment settings from EnvironmentSettings\SettingsFileGenerator.xml
      Environment   machine variables and folders on this server (Set-B2BEnvironment.ps1)
      Dns           CNAME records for the public and internal host names (Install-Dns.ps1)
      Database      VanArsdelIntegration schema, procedures, security, purge job and seed (Install-Database.ps1)
      Sso           SSO configuration store application VanArsdel.Integration (Install-SsoSettings.ps1)
      BizTalk       BizTalk application, GAC, bindings and partner passwords (Install-BizTalkApplication.ps1)
      Rules         Business Rules policies and vocabularies (Install-BusinessRules.ps1)
      Bam           BAM activities, views, online window, reporting views (Install-Bam.ps1)
      Parties       BizTalk parties and EDI/AS2 agreements from the partner list (Install-Parties.ps1)
      Websites      order gateway and operations portal on the web servers (Install-Websites.ps1)
      Tracking      customer tracking site on the DMZ web server (Install-TrackingSite.ps1)
      SlaMonitor    SLA monitor Windows service (Install-SlaMonitor.ps1)

    Run it on a BizTalk server of the group, from an elevated prompt, as a member of the BizTalk Server
    Administrators and SSO Administrators groups and sysadmin on the SQL Server. Steps that target other servers
    (Websites, Tracking, Dns) use PowerShell remoting.

.EXAMPLE
    .\Deploy-B2BIntegration.ps1 -Environment LAB -SecretsFile \\b2bfs01\deploy$\vault\lab-secrets.json

.EXAMPLE
    .\Deploy-B2BIntegration.ps1 -Environment PROD -SecretsFile .\prod-secrets.json -Steps BizTalk,Rules
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('DEV', 'LAB', 'TEST', 'PROD')]
    [string] $Environment,

    [string] $SecretsFile,

    [ValidateSet('Settings', 'Environment', 'Dns', 'Database', 'Sso', 'BizTalk', 'Rules', 'Bam', 'Parties', 'Websites', 'Tracking', 'SlaMonitor')]
    [string[]] $Steps = @('Settings', 'Environment', 'Dns', 'Database', 'Sso', 'BizTalk', 'Rules', 'Bam', 'Parties', 'Websites', 'Tracking', 'SlaMonitor'),

    [string] $Configuration = 'Release',

    [switch] $KeepSecretsFile
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'scripts\B2BDeploy.psm1') -Force

if (-not (Test-B2BAdministrator)) { throw 'Run the deployment from an elevated PowerShell prompt.' }

$log = Initialize-B2BLog -Environment $Environment
$exported = Join-Path $PSScriptRoot 'EnvironmentSettings\Exported'
$settingsFile = Join-Path $exported ('{0}_settings.xml' -f $Environment)

if ($Steps -contains 'Settings' -or -not (Test-Path $settingsFile)) {
    Write-B2BLog 'Settings' -Level Step
    Invoke-B2BTool @('export-settings', '--workbook', (Join-Path $PSScriptRoot 'EnvironmentSettings\SettingsFileGenerator.xml'), '--out', $exported)
}

$settings = Get-B2BSettings -Path $settingsFile
$secrets = Get-B2BSecrets -Path $SecretsFile
$common = @{ Environment = $Environment; Settings = $settings; Secrets = $secrets }

$order = @(
    @{ Step = 'Environment'; Script = 'Set-B2BEnvironment.ps1' },
    @{ Step = 'Dns';         Script = 'Install-Dns.ps1' },
    @{ Step = 'Database';    Script = 'Install-Database.ps1' },
    @{ Step = 'Sso';         Script = 'Install-SsoSettings.ps1' },
    @{ Step = 'BizTalk';     Script = 'Install-BizTalkApplication.ps1' },
    @{ Step = 'Rules';       Script = 'Install-BusinessRules.ps1' },
    @{ Step = 'Bam';         Script = 'Install-Bam.ps1' },
    @{ Step = 'Parties';     Script = 'Install-Parties.ps1' },
    @{ Step = 'Websites';    Script = 'Install-Websites.ps1' },
    @{ Step = 'Tracking';    Script = 'Install-TrackingSite.ps1' },
    @{ Step = 'SlaMonitor';  Script = 'Install-SlaMonitor.ps1' }
)

$started = Get-Date
try {
    foreach ($s in $order | Where-Object { $Steps -contains $_.Step }) {
        Write-B2BLog $s.Step -Level Step
        $script = Join-Path $PSScriptRoot ('scripts\' + $s.Script)
        if ($PSCmdlet.ShouldProcess($Environment, $s.Step)) {
            $extra = @{}
            if ($s.Step -in @('BizTalk')) { $extra.SettingsFile = $settingsFile; $extra.Configuration = $Configuration }
            & $script @common @extra
        }
    }
    Write-B2BLog ('Deployment finished in {0:mm\:ss}. Log: {1}' -f ((Get-Date) - $started), $log)
}
catch {
    Write-B2BLog $_.Exception.Message -Level Error
    Write-B2BLog ('Deployment stopped. Log: {0}' -f $log) -Level Error
    throw
}
finally {
    if ($SecretsFile -and -not $KeepSecretsFile -and (Test-Path $SecretsFile)) {
        Remove-Item -Path $SecretsFile -Force
        Write-B2BLog 'Secrets file deleted.'
    }
}
