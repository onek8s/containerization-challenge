<#
    SSO configuration store application VanArsdel.Integration (identifier ConfigProperties) with the SSO_
    settings of the environment and the ERP client secret. Only SSO values go to the temporary secrets file,
    which is written to the user's temp folder and deleted right after b2bdeploy has read it.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets,
    [string] $SettingsFile = (Join-Path (Split-Path -Parent $PSScriptRoot) "EnvironmentSettings\Exported\${Environment}_settings.xml")
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$ssoSecrets = Join-Path $env:TEMP ('b2b-sso-{0}.json' -f [guid]::NewGuid().ToString('N'))
try {
    @{ ErpClientSecret = (Get-B2BSecret $Secrets 'ErpClientSecret') } | ConvertTo-Json | Set-Content -Path $ssoSecrets -Encoding UTF8
    Invoke-B2BTool @('sso', '--settings', $SettingsFile, '--secrets', $ssoSecrets, '--user-group', $Settings.HostUsersGroup, '--admin-group', $Settings.AdminGroup)
}
finally {
    Remove-Item -Path $ssoSecrets -Force -ErrorAction SilentlyContinue
}

# the runtime caches the values for ten minutes; the BizTalk step restarts the host instances anyway
Write-B2BLog 'SSO application VanArsdel.Integration updated.'
