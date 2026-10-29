<#
.SYNOPSIS
    Service principal names and constrained delegation for Credit Desk.

.DESCRIPTION
    Run once by a domain administrator (requires the ActiveDirectory module).

    Sign-in chain:
      browser --Negotiate--> IIS app pool (svc-creditdesk-web)
              --net.tcp, delegated--> credit service (svc-creditdesk-app)
              --SQL, delegated--> SQL Server (sql01)

    The web site's IIS app pool account may delegate to the credit service SPN, and the credit
    service account may delegate to the SQL Server SPN. Both use Kerberos only.
#>
[CmdletBinding()]
param(
    [string] $Domain = 'FABRIKAM',
    [string] $DnsZone = 'fabrikam.local',
    [string] $WebAccount = 'svc-creditdesk-web',
    [string] $AppAccount = 'svc-creditdesk-app',
    [string] $DocAccount = 'svc-creditdesk-doc',
    [string] $SqlAccount = 'svc-sql01',
    [int] $SqlPort = 1433
)

$ErrorActionPreference = 'Stop'
Import-Module ActiveDirectory

$webSpn = "HTTP/creditdesk.$DnsZone"
$appSpn = "CreditDesk/credit01.$DnsZone"
$sqlSpns = @("MSSQLSvc/sql01.$DnsZone", "MSSQLSvc/sql01.${DnsZone}:$SqlPort")

foreach ($account in $WebAccount, $AppAccount, $DocAccount) {
    if (-not (Get-ADUser -Filter "SamAccountName -eq '$account'" -ErrorAction SilentlyContinue)) {
        throw "Service account $Domain\$account does not exist. Create it first (password never expires, not interactive)."
    }
}

function Set-Spn([string] $account, [string[]] $spns) {
    foreach ($spn in $spns) {
        # -S checks for duplicates across the forest before adding.
        & setspn.exe -S $spn "$Domain\$account" | Out-Host
    }
}

Set-Spn $WebAccount @($webSpn, "HTTP/creditdesk")
Set-Spn $AppAccount @($appSpn, "CreditDesk/credit01")
Set-Spn $SqlAccount $sqlSpns

# Constrained delegation, Kerberos only (no protocol transition).
Set-ADUser $WebAccount -Replace @{ 'msDS-AllowedToDelegateTo' = @($appSpn, 'CreditDesk/credit01') }
Set-ADAccountControl $WebAccount -TrustedForDelegation $false -TrustedToAuthForDelegation $false

Set-ADUser $AppAccount -Replace @{ 'msDS-AllowedToDelegateTo' = $sqlSpns }
Set-ADAccountControl $AppAccount -TrustedForDelegation $false -TrustedToAuthForDelegation $false

# Application role groups, see README.
foreach ($g in 'CreditDesk Sales', 'CreditDesk Controllers', 'CreditDesk Finance Managers', 'CreditDesk Committee') {
    if (-not (Get-ADGroup -Filter "Name -eq '$g'" -ErrorAction SilentlyContinue)) {
        New-ADGroup -Name $g -GroupScope Global -GroupCategory Security -Description 'Credit Desk application role'
        Write-Host "Created group $Domain\$g"
    }
}

Write-Host ''
Write-Host 'Done. Users must sign out and in again (or run klist purge) to pick up new group memberships.'
Write-Host 'Check with:  setspn -L' $WebAccount ' / setspn -L' $AppAccount
