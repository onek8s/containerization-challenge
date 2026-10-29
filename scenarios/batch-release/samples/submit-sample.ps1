<#
.SYNOPSIS
    Sends the sample batch T2600418 and its documents to the API, the way the MES does.
.EXAMPLE
    .\submit-sample.ps1 -ApiKey br_xxxxxxxx_yyyy
    .\submit-sample.ps1 -ApiKey br_xxxxxxxx_yyyy -ApiUrl http://release-api.corp.local -BatchNumber T2600419
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ApiKey,
    [string] $ApiUrl = 'https://release-api.corp.local',
    [string] $BatchNumber
)

$ErrorActionPreference = 'Stop'
$headers = @{ 'X-Api-Key' = $ApiKey }

$batch = Get-Content (Join-Path $PSScriptRoot 'T2600418.json') -Raw | ConvertFrom-Json
if ($BatchNumber) {
    $batch.batchNumber = $BatchNumber
    $batch.mesReference = "MES-$BatchNumber"
}
$body = $batch | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "$ApiUrl/api/v1/batches" -Headers $headers -ContentType 'application/json' -Body $body | Out-Host

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.DefaultRequestHeaders.Add('X-Api-Key', $ApiKey)
foreach ($doc in @(@{ Kind = 'ExecutedBatchRecord'; File = 'T2600418-ebr.pdf' }, @{ Kind = 'CertificateOfAnalysis'; File = 'T2600418-coa.pdf' })) {
    $form = New-Object System.Net.Http.MultipartFormDataContent
    $form.Add((New-Object System.Net.Http.StringContent($doc.Kind)), 'kind')
    $bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $doc.File))
    $content = New-Object System.Net.Http.ByteArrayContent(, $bytes)
    $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/pdf')
    $name = $doc.File -replace 'T2600418', $batch.batchNumber
    $form.Add($content, 'file', $name)
    $response = $client.PostAsync("$ApiUrl/api/v1/batches/$($batch.batchNumber)/documents", $form).Result
    Write-Host "$($doc.Kind): $([int]$response.StatusCode) $($response.Content.ReadAsStringAsync().Result)"
}

Invoke-RestMethod -Uri "$ApiUrl/api/v1/batches/$($batch.batchNumber)" -Headers $headers -ErrorAction SilentlyContinue | ConvertTo-Json -Depth 4
