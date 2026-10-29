<#
.SYNOPSIS
    Creates the ADFS application group for Expense Claims: the Angular front end as a public (native)
    client and the Web API as a relying party, with the claims the API expects.
.DESCRIPTION
    Run on the primary ADFS server (Windows Server 2019 or later, AD FS farm behaviour level 4).
    For a lab, AD FS can be installed on the domain controller:
        Install-WindowsFeature ADFS-Federation -IncludeManagementTools
        Install-AdfsFarm -CertificateThumbprint <adfs.corp.local cert> -FederationServiceName adfs.corp.local `
            -GroupServiceAccountIdentifier 'CORP\gmsa-adfs$'
    and adfs.corp.local registered in DNS.

    Tokens issued to the API contain:
        upn          userPrincipalName, matched to the employee register (HR import)
        unique_name  display name
        role         Finance / Administrator from the AD groups given as parameters
    The client id printed at the end goes to install-web.ps1 -ClientId.
.EXAMPLE
    .\new-adfs-application.ps1 -FinanceGroup 'CORP\EXP-Finance' -AdministratorGroup 'CORP\EXP-Administrators'
#>
[CmdletBinding()]
param(
    [string] $Name = 'Expense Claims',
    [string] $SpaUrl = 'https://expenses.corp.local/',
    [string] $ApiIdentifier = 'https://expenses-api.corp.local',
    [Parameter(Mandatory)] [string] $FinanceGroup,
    [Parameter(Mandatory)] [string] $AdministratorGroup,
    [string] $AccessControlPolicy = 'Permit everyone',
    [string] $ClientId = [guid]::NewGuid().ToString(),
    [switch] $AllowLocalDevelopment
)

$ErrorActionPreference = 'Stop'
Import-Module ADFS
Import-Module ActiveDirectory

function Get-GroupSid([string] $Group) {
    $name = $Group.Split('\')[-1]
    (Get-ADGroup -Identity $name).SID.Value
}

$financeSid = Get-GroupSid $FinanceGroup
$adminSid = Get-GroupSid $AdministratorGroup

$rules = @"
@RuleTemplate = "LdapClaims"
@RuleName = "UPN and display name"
c:[Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname", Issuer == "AD AUTHORITY"]
 => issue(store = "Active Directory", types = ("upn", "unique_name"), query = ";userPrincipalName,displayName;{0}", param = c.Value);

@RuleTemplate = "EmitGroupClaims"
@RuleName = "Finance role"
c:[Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/groupsid", Value == "$financeSid", Issuer == "AD AUTHORITY"]
 => issue(Type = "role", Value = "Finance", Issuer = c.Issuer, OriginalIssuer = c.OriginalIssuer, ValueType = c.ValueType);

@RuleTemplate = "EmitGroupClaims"
@RuleName = "Administrator role"
c:[Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/groupsid", Value == "$adminSid", Issuer == "AD AUTHORITY"]
 => issue(Type = "role", Value = "Administrator", Issuer = c.Issuer, OriginalIssuer = c.OriginalIssuer, ValueType = c.ValueType);
"@

if (Get-AdfsApplicationGroup -Name $Name -ErrorAction SilentlyContinue) {
    throw "Application group '$Name' already exists. Remove it first or use another name."
}

$redirects = @($SpaUrl)
if ($AllowLocalDevelopment) { $redirects += 'http://localhost:4200/' }

New-AdfsApplicationGroup -Name $Name | Out-Null
Add-AdfsNativeClientApplication -ApplicationGroupIdentifier $Name -Name "$Name - web" -Identifier $ClientId -RedirectUri $redirects | Out-Null
Add-AdfsWebApiApplication -ApplicationGroupIdentifier $Name -Name "$Name - API" -Identifier $ApiIdentifier `
    -AccessControlPolicyName $AccessControlPolicy -IssuanceTransformRules $rules | Out-Null
Grant-AdfsApplicationPermission -ClientRoleIdentifier $ClientId -ServerRoleIdentifier $ApiIdentifier -ScopeNames 'openid', 'profile', 'allatclaims' | Out-Null

# The SPA calls the token endpoint from the browser.
$origins = @((Get-AdfsResponseHeaders).CORSTrustedOrigins) + ($SpaUrl.TrimEnd('/'))
if ($AllowLocalDevelopment) { $origins += 'http://localhost:4200' }
Set-AdfsResponseHeaders -EnableCORS $true -CORSTrustedOrigins ($origins | Where-Object { $_ } | Select-Object -Unique)

Write-Host "Application group '$Name' created."
Write-Host "Client id: $ClientId"
Write-Host "API identifier (audience): $ApiIdentifier"
