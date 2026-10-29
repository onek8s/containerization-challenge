# Shared helpers for the Southridge deployment scripts (PowerShell 3.0, Windows Server 2008 R2 / 2012).
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$ScenarioRoot = Split-Path $PSScriptRoot -Parent

function Write-Step([string] $Text) { Write-Host "==> $Text" -ForegroundColor Cyan }

function Get-DeploySettings([string] $Environment, [string] $SecretsFile) {
    $file = if ($Environment -eq 'Lab') { 'lab.ps1' } else { 'environment.ps1' }
    $settings = & (Join-Path $PSScriptRoot $file)
    if (-not $settings.ContainsKey('LabStubs')) { $settings.LabStubs = $false }
    $settings.Secrets = if ($SecretsFile) { & $SecretsFile } else { @{} }
    return $settings
}

function Get-Secret($Settings, [string] $Name, [string] $LabDefault) {
    if ($Settings.Secrets.ContainsKey($Name) -and $Settings.Secrets[$Name]) { return $Settings.Secrets[$Name] }
    if ($Settings.LabStubs) { return $LabDefault }
    throw "Secret '$Name' is required for the $($Settings.Name) environment."
}

function Get-LocalPath([string] $Relative) { return Join-Path $env:SystemDrive $Relative }

function New-Folder([string] $Path) {
    if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
    return $Path
}

function Grant-FolderAccess([string] $Path, [string] $Identity, [string] $Rights = 'Modify') {
    New-Folder $Path | Out-Null
    $acl = Get-Acl $Path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($Identity, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

function Get-MSBuild {
    $candidates = @(
        "${env:ProgramFiles(x86)}\MSBuild\12.0\Bin\MSBuild.exe",
        "$env:windir\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw 'MSBuild 12.0 (Visual Studio 2013 build tools) was not found.'
}

# Replaces __TOKEN__ placeholders left in the transformed Web.config files.
function Set-ConfigTokens([string] $File, [hashtable] $Tokens) {
    $text = [IO.File]::ReadAllText($File)
    foreach ($key in $Tokens.Keys) { $text = $text.Replace("__${key}__", [Security.SecurityElement]::Escape([string]$Tokens[$key])) }
    if ($text -match '__[A-Z_]+__') { throw "$File still contains $($Matches[0])." }
    [IO.File]::WriteAllText($File, $text, (New-Object Text.UTF8Encoding $false))
}

# Sets appSettings values and connection strings in an .exe.config (console and service applications have no transforms).
function Set-AppConfig([string] $File, [hashtable] $AppSettings, [hashtable] $ConnectionStrings) {
    [xml] $xml = Get-Content $File
    foreach ($key in $AppSettings.Keys) {
        $node = $xml.SelectSingleNode("/configuration/appSettings/add[@key='$key']")
        if ($node -eq $null) {
            $node = $xml.CreateElement('add')
            $node.SetAttribute('key', $key)
            $xml.configuration.appSettings.AppendChild($node) | Out-Null
        }
        $node.SetAttribute('value', [string]$AppSettings[$key])
    }
    foreach ($name in $ConnectionStrings.Keys) {
        $node = $xml.SelectSingleNode("/configuration/connectionStrings/add[@name='$name']")
        $node.SetAttribute('connectionString', $ConnectionStrings[$name])
    }
    $xml.Save($File)
}

function Get-ConnectionString($Settings, [string] $Database, [string] $User, [string] $Password, [string] $Application) {
    return "Data Source=$($Settings.Sql.Server);Initial Catalog=$Database;User ID=$User;Password=$Password;Application Name=$Application"
}

function Invoke-SqlFile($Settings, [string] $File, [string] $Database = 'master', [string[]] $Variables = @()) {
    $password = Get-Secret $Settings 'SqlAdminPassword' 'Lab!Passw0rd2013'
    $sqlArgs = @('-S', $Settings.Sql.Server, '-U', $Settings.Sql.AdminUser, '-P', $password, '-d', $Database, '-b', '-i', $File)
    foreach ($v in $Variables) { $sqlArgs += @('-v', $v) }
    Write-Host "    $(Split-Path $File -Leaf)"
    & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for $File (exit code $LASTEXITCODE)." }
}
