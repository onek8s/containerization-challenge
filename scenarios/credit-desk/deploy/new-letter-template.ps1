<#
.SYNOPSIS
    Creates a starter CreditLimitLetter.dotx with the bookmarks the letter service fills.

.DESCRIPTION
    Credit Control maintains the real template (letterhead, legal text, signature image). Use this
    script for a new environment and hand the file to Credit Control for layout. Requires Word.
#>
[CmdletBinding()]
param(
    [string] $Path = '\\fs01.fabrikam.local\templates$\CreditDesk\CreditLimitLetter.dotx'
)

$ErrorActionPreference = 'Stop'
$word = New-Object -ComObject Word.Application
try {
    $word.Visible = $false
    $doc = $word.Documents.Add()
    $sel = $word.Selection

    function Field([string] $name) {
        $start = $sel.Range.Start
        $sel.TypeText("[$name]")
        $range = $doc.Range($start, $sel.Range.End)
        $doc.Bookmarks.Add($name, $range) | Out-Null
    }

    $sel.Font.Name = 'Calibri'
    $sel.Font.Size = 11
    $sel.ParagraphFormat.Alignment = 2; Field 'LetterDate'; $sel.TypeParagraph()
    $sel.ParagraphFormat.Alignment = 0
    $sel.TypeParagraph(); Field 'AddressBlock'; $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.TypeText('Our reference: '); Field 'Reference'; $sel.TypeParagraph()
    $sel.TypeText('Account: '); Field 'AccountNumber'; $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.Font.Bold = $true; $sel.TypeText('Credit limit review'); $sel.Font.Bold = $false; $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.TypeText('Dear '); Field 'ContactName'; $sel.TypeText(','); $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.TypeText('Thank you for your continued business with '); Field 'CustomerName'; $sel.TypeText('.'); $sel.TypeParagraph(); $sel.TypeParagraph()
    Field 'DecisionText'; $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.TypeText('Previous credit limit: '); Field 'PreviousLimit'; $sel.TypeParagraph()
    $sel.TypeText('Credit limit: '); Field 'NewLimit'; $sel.TypeParagraph()
    $sel.TypeText('Review date: '); Field 'ValidUntil'; $sel.TypeParagraph(); $sel.TypeParagraph()
    $sel.TypeText('Yours sincerely,'); $sel.TypeParagraph(); $sel.TypeParagraph(); $sel.TypeParagraph()
    Field 'SignedBy'; $sel.TypeParagraph()
    Field 'SignedByTitle'; $sel.TypeParagraph()
    $sel.TypeText('Fabrikam Wholesale, Credit Control')

    New-Item (Split-Path $Path) -ItemType Directory -Force | Out-Null
    $doc.SaveAs2($Path, 14)   # wdFormatXMLTemplate
    $doc.Close()
    Write-Host "Template written to $Path"
}
finally {
    $word.Quit()
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($word) | Out-Null
}
