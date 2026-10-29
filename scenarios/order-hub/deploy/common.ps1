# Shared helpers for the Order Hub deployment scripts (dot-source this file).

function Assert-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script in an elevated PowerShell session.'
    }
}

function Resolve-AccountSid([string] $Account) {
    $name = if ($Account.StartsWith('.\')) { "$env:COMPUTERNAME\$($Account.Substring(2))" } else { $Account }
    (New-Object Security.Principal.NTAccount($name)).Translate([Security.Principal.SecurityIdentifier]).Value
}

function Grant-ServiceLogonRight([string] $Account) {
    $sid = Resolve-AccountSid $Account
    $tmp = Join-Path $env:TEMP "orderhub-rights-$([guid]::NewGuid().ToString('N')).inf"
    secedit.exe /export /cfg $tmp /areas USER_RIGHTS | Out-Null
    # The export is UTF-16; read it as such or the existing line is not found and would be replaced.
    $lines = Get-Content $tmp -Encoding Unicode
    $line = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
    if ($line -and $line -like "*$sid*") { Remove-Item $tmp; return }
    $new = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $new) |
        Set-Content $tmp -Encoding Unicode
    secedit.exe /configure /db (Join-Path $env:TEMP 'orderhub-rights.sdb') /cfg $tmp /areas USER_RIGHTS | Out-Null
    Remove-Item $tmp
    Write-Host "Granted 'Log on as a service' to $Account"
}

function New-LocalServiceAccount([string] $Name, [securestring] $Password, [string] $Description) {
    if (-not (Get-LocalUser -Name $Name -ErrorAction SilentlyContinue)) {
        New-LocalUser -Name $Name -Password $Password -Description $Description -PasswordNeverExpires -UserMayNotChangePassword | Out-Null
        Write-Host "Created local user $Name"
    }
    else {
        Set-LocalUser -Name $Name -Password $Password
    }
}

function Grant-FolderAccess([string] $Path, [string] $Account, [string] $Rights = 'Modify') {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    $acl = Get-Acl $Path
    $rule = New-Object Security.AccessControl.FileSystemAccessRule($Account, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

function Set-AppSettings([string] $ConfigPath, [hashtable] $Settings) {
    [xml] $xml = Get-Content $ConfigPath -Raw
    foreach ($key in $Settings.Keys) {
        $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
        if (-not $node) {
            $node = $xml.CreateElement('add')
            $node.SetAttribute('key', $key)
            [void] $xml.configuration.appSettings.AppendChild($node)
        }
        $node.SetAttribute('value', [string] $Settings[$key])
    }
    $xml.Save($ConfigPath)
}

function Set-ConnectionString([string] $ConfigPath, [string] $Name, [string] $Value) {
    [xml] $xml = Get-Content $ConfigPath -Raw
    $node = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq $Name }
    if (-not $node) { throw "Connection string $Name not found in $ConfigPath" }
    $node.SetAttribute('connectionString', $Value)
    $xml.Save($ConfigPath)
}

function ConvertTo-PlainText([securestring] $Value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function New-SqlConnectionString([string] $Server, [string] $Database, [string] $User, [string] $Password, [string] $Application) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $Server
    $b['Initial Catalog'] = $Database
    $b['User ID'] = $User
    $b['Password'] = $Password
    $b['Application Name'] = $Application
    $b.ConnectionString
}
