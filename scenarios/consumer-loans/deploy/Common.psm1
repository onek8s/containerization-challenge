Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Write-Step([string] $Text) { Write-Host "==> $Text" -ForegroundColor Cyan }

function Import-DeployConfig([string] $Path, [string] $SecretsPath) {
    $config = Import-PowerShellDataFile -Path $Path
    $config.Secrets = if ($SecretsPath) { Import-PowerShellDataFile -Path $SecretsPath } else { @{} }
    return $config
}

function Get-Secret($Config, [string] $Name, [string] $Default = '') {
    if ($Config.Secrets.ContainsKey($Name) -and $Config.Secrets[$Name]) { return $Config.Secrets[$Name] }
    return $Default
}

function Test-Lab($Config) { return [bool]$Config.LabStubs }

function Get-HostName($Config, [string] $Name) { return "$Name.$($Config.DnsZone)" }

# Writes appsettings.Production.json next to the published application (hashtable -> nested JSON).
function Write-ProductionSettings([string] $Folder, [hashtable] $Settings) {
    $target = Join-Path $Folder 'appsettings.Production.json'
    $Settings | ConvertTo-Json -Depth 8 | Set-Content -Path $target -Encoding UTF8
    Write-Host "    $target"
}

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

# Adds an account to a user right (SeServiceLogonRight, SeBatchLogonRight) without touching other assignments:
# the current policy is exported, the account appended to the existing list, and only that right is applied back.
function Grant-UserRight([string] $Account, [string] $Right) {
    if ($Account -in @('LocalSystem', 'NT AUTHORITY\SYSTEM')) { return }
    $sid = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $work = Join-Path $env:TEMP ("secedit-" + [guid]::NewGuid().ToString('N'))
    New-Folder $work | Out-Null
    $export = Join-Path $work 'export.inf'
    $import = Join-Path $work 'import.inf'
    $db = Join-Path $work 'secedit.sdb'
    try {
        secedit /export /cfg $export /areas USER_RIGHTS /quiet | Out-Null
        $line = Get-Content $export -Encoding Unicode | Where-Object { $_ -match "^$Right\s*=" }
        $current = if ($line) { ($line -split '=', 2)[1].Trim() } else { '' }
        if ($current -split ',' | Where-Object { $_.Trim() -eq "*$sid" }) { Write-Host "    $Account already has $Right"; return }
        $merged = if ($current) { "$current,*$sid" } else { "*$sid" }
        @(
            '[Unicode]', 'Unicode=yes',
            '[Version]', 'signature="$CHICAGO$"', 'Revision=1',
            '[Privilege Rights]', "$Right = $merged"
        ) | Set-Content -Path $import -Encoding Unicode
        secedit /configure /db $db /cfg $import /areas USER_RIGHTS /quiet | Out-Null
        Write-Host "    $Right granted to $Account"
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-SqlConnectionString($Config, [string] $Database, [string] $User, [string] $Application) {
    $password = Get-Secret $Config "Sql.$User"
    return "Server=$($Config.Sql.Server);Database=$Database;User Id=$User;Password=$password;Encrypt=True;TrustServerCertificate=$(if (Test-Lab $Config) { 'True' } else { 'False' });Application Name=$Application"
}

function Get-CoreSettings($Config, [int] $MaxConnections = 4) {
    if ($Config.Core.Provider -eq 'Simulator') {
        return @{ Provider = 'Simulator'; SimulatorDatabase = $Config.Core.SimulatorDatabase }
    }
    $system = $Config.Core.SystemName
    return @{
        Provider             = 'IbmI'
        SystemName           = $system
        UseKerberos          = $Config.Core.UseKerberos
        MaxConnections       = $MaxConnections
        OdbcConnectionString = "Driver={IBM i Access ODBC Driver};System=$system;Signon=4;Naming=1;DefaultLibraries=LNLIB,CIFLIB,RATLIB,DPLIB;CCSID=1208;TranslateBinary=1;CommitMode=0;"
        OleDbConnectionString = "Provider=IBMDA400;Data Source=$system;Default Collection=LNLIB;Convert Date Time To Char=FALSE;Catalog Library List=LNLIB,CIFLIB;"
    }
}

function Get-LogSettings([string] $Folder) {
    return @{ variables = @{ logDir = $Folder } }
}

Export-ModuleMember -Function *
