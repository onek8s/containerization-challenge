<#
.SYNOPSIS
    Sets the release policy (who may read, review, release, reject and hold) of a product family.
.DESCRIPTION
    Groups are resolved to SIDs and written as an SDDL security descriptor. Rights are bit flags:
    ReadRecord 0x1, Review 0x2, Release 0x4, Reject 0x8, Hold 0x10, ManageSigners 0x20, ManagePolicy 0x40.
    Use -DenyRelease for accounts that may never release (for example production managers who are
    also in a QA group).
.EXAMPLE
    .\set-release-policy.ps1 -Family OSD -Readers 'CORP\BR-QA-Readers' -Reviewers 'CORP\BR-QA-Reviewers' `
        -QualifiedPersons 'CORP\BR-QP-OSD' -Administrators 'CORP\BR-QA-Systems'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Family,
    [string[]] $Readers = @(),
    [string[]] $Reviewers = @(),
    [string[]] $QualifiedPersons = @(),
    [string[]] $Administrators = @(),
    [string[]] $DenyRelease = @(),
    [string] $SqlInstance = 'release-sql.corp.local',
    [string] $DatabaseName = 'BatchRelease'
)

$ErrorActionPreference = 'Stop'

function Sid([string] $Name) {
    (New-Object System.Security.Principal.NTAccount($Name)).Translate([System.Security.Principal.SecurityIdentifier]).Value
}

$deny = foreach ($n in $DenyRelease) { "(D;;0x4;;;$(Sid $n))" }
$allow = @()
$allow += foreach ($n in $Readers) { "(A;;0x1;;;$(Sid $n))" }
$allow += foreach ($n in $Reviewers) { "(A;;0x13;;;$(Sid $n))" }
$allow += foreach ($n in $QualifiedPersons) { "(A;;0x1d;;;$(Sid $n))" }
$allow += foreach ($n in $Administrators) { "(A;;0x61;;;$(Sid $n))" }
if (-not $allow) { throw 'Give at least one group.' }
$sddl = 'O:BAG:SYD:P' + ($deny -join '') + ($allow -join '')

# Validate before saving.
New-Object System.Security.AccessControl.RawSecurityDescriptor($sddl) | Out-Null

$connection = New-Object System.Data.SqlClient.SqlConnection("Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true")
$connection.Open()
try {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'UPDATE br.ProductFamily SET PolicySddl = @sddl, PolicyChangedUtc = SYSUTCDATETIME(), PolicyChangedBy = SUSER_SNAME() WHERE Code = @code'
    [void]$cmd.Parameters.AddWithValue('@sddl', $sddl)
    [void]$cmd.Parameters.AddWithValue('@code', $Family)
    if ($cmd.ExecuteNonQuery() -ne 1) { throw "Product family $Family does not exist." }
}
finally {
    $connection.Dispose()
}
Write-Host "Release policy of $Family set: $sddl"
