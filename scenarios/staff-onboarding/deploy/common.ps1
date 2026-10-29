# Shared helpers for the install scripts.

function Grant-ServiceLogonRight([string] $Account) {
    $sid = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $tmp = [System.IO.Path]::GetTempFileName()
    secedit.exe /export /cfg $tmp /areas USER_RIGHTS | Out-Null
    # secedit writes UTF-16 without a byte order mark into an existing file; without -Encoding the line is not found
    # and the right would be replaced with this account only.
    $lines = Get-Content $tmp -Encoding Unicode
    $line = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
    if ($line -and $line -like "*$sid*") { Remove-Item $tmp; return }
    $new = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $new) | Set-Content $tmp -Encoding Unicode
    secedit.exe /configure /db "$env:TEMP\so-rights.sdb" /cfg $tmp /areas USER_RIGHTS | Out-Null
    Remove-Item $tmp
    Write-Host "Granted 'Log on as a service' to $Account"
}

function Install-Gmsa([string] $Name) {
    Import-Module ActiveDirectory
    Install-ADServiceAccount -Identity $Name
    if (-not (Test-ADServiceAccount -Identity $Name)) { throw "This computer cannot use the gMSA $Name." }
}

function Ensure-EventSource([string] $Source) {
    if (-not [System.Diagnostics.EventLog]::SourceExists($Source)) {
        New-EventLog -LogName Application -Source $Source
    }
}

function Install-WindowsServiceWithGmsa([string] $Name, [string] $DisplayName, [string] $Description, [string] $BinPath, [string] $Account) {
    $existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.Status -ne 'Stopped') { Stop-Service $Name -Force }
        sc.exe config $Name binPath= "$BinPath" obj= "$Account" password= "" | Out-Host
    }
    else {
        sc.exe create $Name binPath= "$BinPath" obj= "$Account" password= "" start= delayed-auto DisplayName= "$DisplayName" | Out-Host
    }
    sc.exe description $Name "$Description" | Out-Host
    sc.exe failure $Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Host
    sc.exe failureflag $Name 1 | Out-Host
}
