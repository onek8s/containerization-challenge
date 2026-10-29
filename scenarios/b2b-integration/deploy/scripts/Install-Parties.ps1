<#
    BizTalk parties, business profiles and EDI/AS2 agreement aliases from the partner list (the same partners.json
    the integration database seed is generated from). Certificates for AS2 signing and encryption are installed
    by the server team in the Other People store of the B2BAS2Host account; the agreements only reference them.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$root = Get-B2BDeployRoot
$partners = @((Join-Path $root 'partners.json'), (Join-Path $root '..\samples\seed\partners.json')) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $partners) { throw 'partners.json not found next to the deployment or in samples\seed.' }

$group = Get-B2BBizTalkGroup
Invoke-B2BTool @('parties', '--partners', $partners, '--server', $group.Server, '--database', $group.Database)
