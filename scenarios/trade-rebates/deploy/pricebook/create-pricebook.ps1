<#
.SYNOPSIS
    Creates the Access price book (PriceBook.accdb) with tables NetPrice and PriceGroup and loads the CSV files next to this script.
.DESCRIPTION
    Sales operations maintain the price book in Access afterwards. Needs the Microsoft Access Database Engine 2016 (x64)
    and a 64-bit PowerShell.
.EXAMPLE
    .\create-pricebook.ps1 -Path '\\rebates-files.corp.local\Rebates$\PriceBook\PriceBook.accdb'
#>
[CmdletBinding()]
param(
    [string] $Path = '\\rebates-files.corp.local\Rebates$\PriceBook\PriceBook.accdb',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitProcess) { throw 'Run this script in 64-bit PowerShell.' }

if (Test-Path $Path) {
    if (-not $Force) { throw "$Path exists. Use -Force to recreate it." }
    Remove-Item $Path
}

$cs = "Provider=Microsoft.ACE.OLEDB.16.0;Data Source=$Path"
$catalog = New-Object -ComObject ADOX.Catalog
$catalog.Create($cs) | Out-Null
$conn = $catalog.ActiveConnection

function Exec([string] $sql) { $conn.Execute($sql) | Out-Null }

Exec 'CREATE TABLE PriceGroup (PriceGroup TEXT(10) NOT NULL, DistributorCode TEXT(20) NOT NULL, Description TEXT(100), CONSTRAINT PK_PriceGroup PRIMARY KEY (PriceGroup, DistributorCode))'
Exec 'CREATE TABLE NetPrice (EAN TEXT(14) NOT NULL, PriceGroup TEXT(10) NOT NULL, ValidFrom DATETIME NOT NULL, ValidTo DATETIME, NetPrice CURRENCY NOT NULL, CONSTRAINT PK_NetPrice PRIMARY KEY (EAN, PriceGroup, ValidFrom))'

foreach ($row in Import-Csv (Join-Path $PSScriptRoot 'PriceGroup.csv') -Delimiter ';') {
    Exec ("INSERT INTO PriceGroup (PriceGroup, DistributorCode, Description) VALUES ('{0}', '{1}', '{2}')" -f $row.PriceGroup, $row.DistributorCode, $row.Description.Replace("'", "''"))
}
$inv = [Globalization.CultureInfo]::InvariantCulture
foreach ($row in Import-Csv (Join-Path $PSScriptRoot 'NetPrice.csv') -Delimiter ';') {
    $to = if ($row.ValidTo) { "#$($row.ValidTo)#" } else { 'NULL' }
    $price = [decimal]::Parse($row.NetPrice, $inv).ToString($inv)
    Exec ("INSERT INTO NetPrice (EAN, PriceGroup, ValidFrom, ValidTo, NetPrice) VALUES ('{0}', '{1}', #{2}#, {3}, {4})" -f $row.EAN, $row.PriceGroup, $row.ValidFrom, $to, $price)
}

$conn.Close()
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($catalog)
Write-Host "Price book created: $Path"
