<#
.SYNOPSIS
    Creates the group managed service accounts and AD groups used by Fabrikam Housing.
.DESCRIPTION
    Run on a domain controller or a machine with the RSAT Active Directory module, as a domain admin.
    The domain needs a KDS root key (Add-KdsRootKey) before gMSAs can be created.
    -WebServers / -JobServers / -DocumentServers are the computer accounts allowed to retrieve each password.
.EXAMPLE
    .\new-service-accounts.ps1 -WebServers HOST01 -JobServers HOST01 -DocumentServers HOST01
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string[]] $WebServers = @('HOST01'),
    [string[]] $JobServers = @('HOST01'),
    [string[]] $DocumentServers = @('HOST01'),
    [string] $DnsDomain = 'corp.local',
    [string] $GroupsPath
)

$ErrorActionPreference = 'Stop'
Import-Module ActiveDirectory

if (-not (Get-KdsRootKey)) {
    throw 'No KDS root key. Run Add-KdsRootKey -EffectiveTime ((Get-Date).AddHours(-10)) in a lab, or wait 10 hours after Add-KdsRootKey in production.'
}

$accounts = @(
    @{ Name = 'svc-housing-web'; Hosts = $WebServers; Description = 'Fabrikam Housing back office app pool' },
    @{ Name = 'svc-housing-jobs'; Hosts = $JobServers; Description = 'Fabrikam Housing jobs app pool' },
    @{ Name = 'svc-housing-docs'; Hosts = $DocumentServers; Description = 'Fabrikam Housing document service' }
)
foreach ($a in $accounts) {
    $principals = $a.Hosts | ForEach-Object { Get-ADComputer $_ }
    if (Get-ADServiceAccount -Filter "Name -eq '$($a.Name)'") {
        Set-ADServiceAccount $a.Name -PrincipalsAllowedToRetrieveManagedPassword $principals
        Write-Host "Updated $($a.Name)"
    } elseif ($PSCmdlet.ShouldProcess($a.Name, 'New-ADServiceAccount')) {
        New-ADServiceAccount -Name $a.Name -DNSHostName "$($a.Name).$DnsDomain" -Description $a.Description `
            -PrincipalsAllowedToRetrieveManagedPassword $principals -KerberosEncryptionType AES256
        Write-Host "Created $($a.Name)"
    }
}

# Staff roles in the housing applications come from these groups.
$groups = 'HSG-Officers', 'HSG-Repairs', 'HSG-Income', 'HSG-Compliance', 'HSG-Finance', 'HSG-Admins'
foreach ($g in $groups) {
    if (-not (Get-ADGroup -Filter "Name -eq '$g'")) {
        $params = @{ Name = $g; GroupScope = 'Global'; GroupCategory = 'Security'; Description = 'Fabrikam Housing role' }
        if ($GroupsPath) { $params.Path = $GroupsPath }
        if ($PSCmdlet.ShouldProcess($g, 'New-ADGroup')) { New-ADGroup @params; Write-Host "Created group $g" }
    }
}

Write-Host 'On each server run: Install-ADServiceAccount <name>; Test-ADServiceAccount <name>'
