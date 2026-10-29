<#
.SYNOPSIS
    Creates OUs, groups, group managed service accounts, SPNs and OU delegation for Staff Onboarding.
.DESCRIPTION
    Run once as a Domain Admin on a machine with the ActiveDirectory module (RSAT).
    The groups in the access map are created in the Groups OU if they do not exist.
.EXAMPLE
    .\prepare-directory.ps1 -AccessMap ..\deploy\config\access-map.xml -PortalServer WEB01 -WorkflowServer APP01 -ProvisioningServer IDM01
#>
param(
    [string] $BaseOu = 'OU=Litware,DC=corp,DC=local',
    [Parameter(Mandatory)] [string] $AccessMap,
    [Parameter(Mandatory)] [string] $PortalServer,
    [Parameter(Mandatory)] [string] $WorkflowServer,
    [Parameter(Mandatory)] [string] $ProvisioningServer,
    [string] $DnsSuffix = 'corp.local',
    [string] $PortalHostName = 'onboarding',
    [string] $WorkflowHostName = 'onboarding-flow'
)

$ErrorActionPreference = 'Stop'
Import-Module ActiveDirectory
$domain = Get-ADDomain
$netbios = $domain.NetBIOSName

function Ensure-Ou([string] $Name, [string] $Path) {
    $dn = "OU=$Name,$Path"
    if (-not (Get-ADOrganizationalUnit -Filter "DistinguishedName -eq '$dn'" -ErrorAction SilentlyContinue)) {
        New-ADOrganizationalUnit -Name $Name -Path $Path -ProtectedFromAccidentalDeletion $true
        Write-Host "Created $dn"
    }
    $dn
}

function Ensure-Group([string] $Name, [string] $Path) {
    if (-not (Get-ADGroup -Filter "SamAccountName -eq '$Name'" -ErrorAction SilentlyContinue)) {
        New-ADGroup -Name $Name -SamAccountName $Name -GroupScope Global -GroupCategory Security -Path $Path
        Write-Host "Created group $Name"
    }
}

# OUs
$usersOu = Ensure-Ou 'Users' $BaseOu
$staffOu = Ensure-Ou 'Staff' $usersOu
Ensure-Ou 'External' $usersOu | Out-Null
Ensure-Ou 'Disabled' $usersOu | Out-Null
$groupsOu = Ensure-Ou 'Groups' $BaseOu
$serviceOu = Ensure-Ou 'Service Accounts' $BaseOu

[xml] $map = Get-Content -Raw -Path $AccessMap
foreach ($d in $map.accessMap.department) {
    $ouName = ($d.ou -split ',')[0] -replace '^OU=', ''
    Ensure-Ou $ouName $staffOu | Out-Null
}

# Groups from the access map plus the application groups
$names = @($map.SelectNodes('//group') | ForEach-Object { $_.name }) + @(
    'APP-Onboarding-HR', 'APP-Onboarding-IT', 'APP-Onboarding-IT-Leads', 'FS-Home-Admins', 'APP-ServiceDesk-Agents')
foreach ($g in ($names | Sort-Object -Unique)) { Ensure-Group $g $groupsOu }

# Group managed service accounts
if (-not (Get-KdsRootKey)) {
    throw 'No KDS root key. Run Add-KdsRootKey -EffectiveImmediately and wait 10 hours (or use -EffectiveTime in a lab).'
}

$accounts = @(
    @{ Name = 'svc-onbweb'; Host = $PortalServer; Spn = @("HTTP/$PortalHostName.$DnsSuffix", "HTTP/$PortalHostName") },
    @{ Name = 'svc-onbflow'; Host = $WorkflowServer; Spn = @("HTTP/$WorkflowHostName.$DnsSuffix", "HTTP/$WorkflowHostName") },
    @{ Name = 'svc-idm'; Host = $ProvisioningServer; Spn = @() }
)
foreach ($a in $accounts) {
    $computer = Get-ADComputer $a.Host
    if (-not (Get-ADServiceAccount -Filter "Name -eq '$($a.Name)'" -ErrorAction SilentlyContinue)) {
        New-ADServiceAccount -Name $a.Name -DNSHostName "$($a.Name).$DnsSuffix" -Path $serviceOu `
            -PrincipalsAllowedToRetrieveManagedPassword $computer -KerberosEncryptionType AES256
        Write-Host "Created gMSA $($a.Name)"
    }
    foreach ($spn in $a.Spn) { setspn.exe -S $spn "$netbios\$($a.Name)$" | Out-Host }
}

# The provisioning account creates and manages users in the Users OU tree and maintains group membership.
$idm = "$netbios\svc-idm$"
dsacls.exe $usersOu /I:T /G "${idm}:CCDC;user" | Out-Null
dsacls.exe $usersOu /I:S /G "${idm}:GA;;user" | Out-Null
dsacls.exe $usersOu /I:S /G "${idm}:CA;Reset Password;user" | Out-Null
dsacls.exe $groupsOu /I:S /G "${idm}:RPWP;member;group" | Out-Null
Write-Host "Delegated user management on $usersOu and membership on $groupsOu to $idm"

# The portal and workflow host need to read group membership of users (tokenGroups) for role checks.
Add-ADGroupMember -Identity 'Windows Authorization Access Group' -Members (Get-ADServiceAccount svc-onbweb), (Get-ADServiceAccount svc-onbflow)
Write-Host 'Directory prepared.'
