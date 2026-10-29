<#
.SYNOPSIS
    Builds the Angular front end and publishes it as the static IIS site expenses.corp.local.
.DESCRIPTION
    Prerequisites: IIS with Static Content, the IIS URL Rewrite 2.1 module, Node.js 16 or 18 (LTS) and npm
    on the build machine. The site needs no server-side code; the application pool runs with No Managed Code.
    assets\config.json is written from the parameters, so the same build can be deployed to test and production.
.EXAMPLE
    .\install-web.ps1 -CertificateThumbprint 3F2A... -ClientId 0d6f2b4c-... -ApiUrl https://expenses-api.corp.local
    .\install-web.ps1 -AuthMode development -ApiUrl http://expenses-api.corp.local
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $HostName = 'expenses.corp.local',
    [string] $ApiUrl = 'https://expenses-api.corp.local',
    [ValidateSet('adfs', 'development')] [string] $AuthMode = 'adfs',
    [string] $Authority = 'https://adfs.corp.local/adfs',
    [string] $ClientId = '',
    [string] $Resource = 'https://expenses-api.corp.local',
    [string] $CertificateThumbprint,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if (-not (Get-WebGlobalModule -Name 'RewriteModule' -ErrorAction SilentlyContinue)) {
    throw 'The IIS URL Rewrite module is not installed.'
}
if ($AuthMode -eq 'adfs' -and -not $ClientId) {
    throw 'ClientId is required with ADFS sign-in (see new-adfs-application.ps1).'
}

$project = Join-Path $PSScriptRoot '..\src\expenses-web'
$target = Join-Path $SiteRoot 'expenses'

if (-not $SkipBuild) {
    Push-Location $project
    try {
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Angular build failed.' }
    } finally {
        Pop-Location
    }
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Set-Content -Path (Join-Path $target 'app_offline.htm') -Value '<p>Expense Claims is being updated.</p>'
Get-ChildItem $target -Exclude 'app_offline.htm' | Remove-Item -Recurse -Force
Copy-Item (Join-Path $project 'dist\expenses-web\*') $target -Recurse -Force

@{
    apiUrl = $ApiUrl.TrimEnd('/')
    auth = @{
        mode = $AuthMode
        authority = $Authority
        clientId = $ClientId
        resource = $Resource
        scope = 'openid profile allatclaims'
    }
} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $target 'assets\config.json') -Encoding UTF8
Remove-Item (Join-Path $target 'app_offline.htm')

if (-not (Test-Path 'IIS:\AppPools\expenses')) { New-WebAppPool -Name 'expenses' | Out-Null }
Set-ItemProperty 'IIS:\AppPools\expenses' -Name managedRuntimeVersion -Value ''

if (-not (Get-Website -Name 'expenses')) {
    New-Website -Name 'expenses' -PhysicalPath $target -ApplicationPool 'expenses' -HostHeader $HostName -Port 80 | Out-Null
} else {
    Set-ItemProperty 'IIS:\Sites\expenses' -Name physicalPath -Value $target
}
if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name 'expenses' -Protocol https -HostHeader $HostName)) {
        New-WebBinding -Name 'expenses' -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    }
    (Get-WebBinding -Name 'expenses' -Protocol https -HostHeader $HostName).AddSslCertificate($CertificateThumbprint, 'My')
} else {
    # Without a certificate the HTTPS redirect rule would make the site unreachable.
    $config = Join-Path $target 'web.config'
    [xml] $xml = Get-Content $config
    $rule = $xml.configuration.'system.webServer'.rewrite.rules.rule | Where-Object name -eq 'HTTPS'
    if ($rule) { [void] $rule.ParentNode.RemoveChild($rule); $xml.Save($config) }
}

$scheme = if ($CertificateThumbprint) { 'https' } else { 'http' }
Write-Host "Expense Claims: ${scheme}://$HostName/ (sign-in: $AuthMode, API: $ApiUrl)"
