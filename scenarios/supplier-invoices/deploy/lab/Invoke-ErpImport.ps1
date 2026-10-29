<#
.SYNOPSIS
    Test stand-in for the ERP job APIMP01: reads APIMP_*.dat files from the import folder, creates vouchers
    in the ERP test database and writes APACK_*.dat result files to the export folder.
    Schedule it every few minutes on the lab server, or run it by hand.
.EXAMPLE
    .\Invoke-ErpImport.ps1 -ImportFolder \\erp01.corp.local\Import\AP -ExportFolder \\erp01.corp.local\Export\AP -SqlServer erp01.corp.local
#>
param(
    [string]$ImportFolder = '\\erp01.corp.local\Import\AP',
    [string]$ExportFolder = '\\erp01.corp.local\Export\AP',
    [string]$SqlServer = 'erp01.corp.local',
    [string[]]$BlockedVendors = @()
)

$ErrorActionPreference = 'Stop'
$encoding = [System.Text.Encoding]::GetEncoding(850)
New-Item -ItemType Directory -Force -Path (Join-Path $ImportFolder 'done') | Out-Null

foreach ($file in Get-ChildItem $ImportFolder -Filter 'APIMP_*.dat') {
    $lines = [System.IO.File]::ReadAllLines($file.FullName, $encoding)
    $results = New-Object System.Collections.Generic.List[string]
    $ok = 0; $failed = 0

    foreach ($line in $lines | Where-Object { $_.StartsWith('API') }) {
        $invoiceId = [long]$line.Substring(3, 10)
        $vendor = $line.Substring(13, 10).Trim()
        if ($BlockedVendors -contains $vendor) {
            $results.Add("APA;$invoiceId;ER;;Vendor $vendor is blocked for payments")
            $failed++
            continue
        }
        $voucher = 'AP{0:yyyy}-{1:D6}' -f (Get-Date), (Get-Random -Minimum 100000 -Maximum 999999)
        $results.Add("APA;$invoiceId;OK;$voucher;")
        $ok++
    }

    # Invoiced quantities on the order lines.
    foreach ($line in $lines | Where-Object { $_.StartsWith('APL') }) {
        $po = $line.Substring(48, 10).Trim()
        $poLine = $line.Substring(58, 4).Trim()
        if (-not $po -or -not $poLine) { continue }
        $qty = [decimal]$line.Substring(62, 11) / 1000
        if ($line[73] -eq '-') { $qty = -$qty }
        $sql = "UPDATE erp.PurchaseOrderLine SET InvoicedQty = InvoicedQty + $qty WHERE Company = 'WT01' AND PoNo = '$po' AND [LineNo] = $([int]$poLine)"
        sqlcmd -S $SqlServer -E -d WingtipErp -b -Q $sql | Out-Null
    }

    $results.Insert(0, "APS;$($ok + $failed);$ok;$failed")
    $ack = Join-Path $ExportFolder ($file.Name -replace '^APIMP_', 'APACK_')
    [System.IO.File]::WriteAllLines($ack, $results, $encoding)
    Move-Item $file.FullName (Join-Path $ImportFolder "done\$($file.Name)") -Force
    Write-Host "$($file.Name): $ok posted, $failed rejected -> $ack"
}
