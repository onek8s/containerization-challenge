<#
.SYNOPSIS
    Creates the BatchRelease database, schema, reference data and logins.
.EXAMPLE
    .\install-database.ps1 -SqlInstance release-sql.corp.local
.EXAMPLE
    .\install-database.ps1 -SqlInstance 'host01\SQLEXPRESS' -WebAccount 'CORP\svc-release-web' -ApiAccount 'CORP\svc-release-api' `
        -SigningAccount 'CORP\svc-release-sign' -AdminAccount 'CORP\BR-QA-Systems'
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'release-sql.corp.local',
    [string] $DatabaseName = 'BatchRelease',
    [string] $WebAccount = 'CORP\svc-release-web',
    [string] $ApiAccount = 'CORP\svc-release-api',
    [string] $SigningAccount = 'CORP\svc-release-sign',
    [string] $AdminAccount = 'CORP\BR-QA-Systems'
)

$ErrorActionPreference = 'Stop'
$db = Join-Path $PSScriptRoot '..\db'

function Invoke-Script([string] $File, [string] $Database, [string[]] $Variables) {
    $sqlArgs = @('-S', $SqlInstance, '-E', '-b', '-I', '-i', (Join-Path $db $File))
    if ($Database) { $sqlArgs += @('-d', $Database) }
    if ($Variables) { $sqlArgs += '-v'; $sqlArgs += $Variables }
    & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "$File failed." }
}

Invoke-Script '00-create-database.sql' 'master' @("DatabaseName=$DatabaseName")
Invoke-Script '01-schema.sql' $DatabaseName
Invoke-Script '02-reference-data.sql' $DatabaseName
Invoke-Script '03-logins.sql' $DatabaseName @("WebAccount=$WebAccount", "ApiAccount=$ApiAccount", "SigningAccount=$SigningAccount", "AdminAccount=$AdminAccount")
Write-Host "Database $DatabaseName on $SqlInstance is ready. Set the release policies with set-release-policy.ps1."
