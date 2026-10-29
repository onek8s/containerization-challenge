<#
.SYNOPSIS
    Creates the ADFS application groups for the monitor site and the sponsor API.
.DESCRIPTION
    Run on the ADFS server (Windows Server 2019 or later). The monitor site signs CRO staff in with
    OpenID Connect (confidential client, authorization code + PKCE) and gets AD group names in the
    "group" claim. Sponsor systems call the sponsor API with client credentials (one server application
    per sponsor, certificate or secret).
.EXAMPLE
    .\configure-adfs.ps1 -MonitorUrl https://monitor.corp.local -SponsorApiUrl https://sponsor-api.corp.local
#>
param(
    [string] $MonitorUrl = 'https://monitor.corp.local',
    [string] $SponsorApiUrl = 'https://sponsor-api.corp.local',
    [string] $MonitorClientId = 'a7d52f0e-3b8c-4e5a-9a1f-6c2b84d1e903',
    [string[]] $SponsorClients = @('tailspin-cdms', 'tailspin-dsmb')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-AdfsApplicationGroup -Name 'Study Data Monitoring' -ErrorAction SilentlyContinue)) {
    New-AdfsApplicationGroup -Name 'Study Data Monitoring' | Out-Null
    $server = Add-AdfsServerApplication -ApplicationGroupIdentifier 'Study Data Monitoring' -Name 'Study Data Monitoring - web' `
        -Identifier $MonitorClientId -RedirectUri "$MonitorUrl/signin-oidc" -GenerateClientSecret -PassThru
    Write-Host "Client secret of the monitor site (store it for install-web.ps1): $($server.ClientSecret)"
    Add-AdfsWebApiApplication -ApplicationGroupIdentifier 'Study Data Monitoring' -Name 'Study Data Monitoring - API' -Identifier $MonitorClientId `
        -AccessControlPolicyName 'Permit everyone' -IssuanceTransformRules @'
@RuleTemplate = "LdapClaims"
@RuleName = "UPN and groups"
c:[Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname", Issuer == "AD AUTHORITY"]
 => issue(store = "Active Directory", types = ("upn", "group", "name"), query = ";userPrincipalName,tokenGroups,displayName;{0}", param = c.Value);
'@ | Out-Null
    Grant-AdfsApplicationPermission -ClientRoleIdentifier $MonitorClientId -ServerRoleIdentifier $MonitorClientId -ScopeNames openid, profile, allatclaims
}

if (-not (Get-AdfsApplicationGroup -Name 'Study Data Sponsor API' -ErrorAction SilentlyContinue)) {
    New-AdfsApplicationGroup -Name 'Study Data Sponsor API' | Out-Null
    Add-AdfsWebApiApplication -ApplicationGroupIdentifier 'Study Data Sponsor API' -Name 'Study Data Sponsor API' -Identifier "$SponsorApiUrl/" `
        -AccessControlPolicyName 'Permit everyone' -IssuanceTransformRules @'
@RuleName = "Client id"
c:[Type == "http://schemas.microsoft.com/2014/01/clientcontext/claims/appid"] => issue(Type = "appid", Value = c.Value);
'@ | Out-Null
    foreach ($client in $SponsorClients) {
        $app = Add-AdfsServerApplication -ApplicationGroupIdentifier 'Study Data Sponsor API' -Name "Sponsor $client" -Identifier $client -GenerateClientSecret -PassThru
        Grant-AdfsApplicationPermission -ClientRoleIdentifier $client -ServerRoleIdentifier "$SponsorApiUrl/" -ScopeNames openid
        Write-Host "Client secret for $client (give it to the sponsor): $($app.ClientSecret)"
    }
}
